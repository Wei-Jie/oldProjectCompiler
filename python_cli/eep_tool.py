#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Infolight EEP (.NET Framework / VS2005) 專案編譯、自動修復與即時監控工具 (Python 版)
支援：
1. 自動呼叫同目錄下的 eepc.exe 或使用純 Python 實作規則 1 ~ 規則 5 的診斷與修復
2. 支援 `watch` 檔案變更監控模式：當 Claude Code 修改並儲存 .cs 時，自動觸發編譯並更新診斷報告
"""

import argparse
import os
import re
import shutil
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Dict, List, Optional, Tuple


def find_eepc_exe() -> Optional[Path]:
    """尋找可攜式套件中的 eepc.exe"""
    script_dir = Path(__file__).resolve().parent
    candidates = [
        script_dir / "eepc.exe",
        script_dir.parent / "dist" / "EEPCompiler_Portable" / "eepc.exe",
        Path.cwd() / "eepc.exe",
        Path.cwd() / "dist" / "EEPCompiler_Portable" / "eepc.exe",
    ]
    for c in candidates:
        if c.is_file():
            return c
    return None


def find_msbuild_exe() -> Optional[Path]:
    """規則 1：尋找本機安裝的 MSBuild.exe（禁止使用 dotnet build）"""
    prog_x86 = os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")
    prog = os.environ.get("ProgramFiles", r"C:\Program Files")

    search_patterns = [
        Path(prog) / "Microsoft Visual Studio" / "2022",
        Path(prog_x86) / "Microsoft Visual Studio" / "2022",
        Path(prog_x86) / "Microsoft Visual Studio" / "2019",
        Path(prog_x86) / "Microsoft Visual Studio" / "2017",
    ]
    editions = ["Enterprise", "Professional", "Community", "BuildTools"]

    for root in search_patterns:
        if not root.is_dir():
            continue
        for ed in editions:
            p1 = root / ed / "MSBuild" / "Current" / "Bin" / "MSBuild.exe"
            if p1.is_file():
                return p1
            p2 = root / ed / "MSBuild" / "15.0" / "Bin" / "MSBuild.exe"
            if p2.is_file():
                return p2

    fallbacks = [
        Path(prog_x86) / "MSBuild" / "14.0" / "Bin" / "MSBuild.exe",
        Path(prog_x86) / "MSBuild" / "12.0" / "Bin" / "MSBuild.exe",
        Path(r"C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"),
        Path(r"C:\Windows\Microsoft.NET\Framework\v3.5\MSBuild.exe"),
        Path(r"C:\Windows\Microsoft.NET\Framework\v2.0.50727\MSBuild.exe"),
    ]
    for fb in fallbacks:
        if fb.is_file():
            return fb
    return None


def run_via_eepc(eepc_path: Path, raw_args: List[str]) -> int:
    """委派給雙核心 eepc.exe 執行並確保 UTF-8 輸出"""
    cmd = [str(eepc_path)] + raw_args
    proc = subprocess.run(cmd, text=True, encoding="utf-8", errors="replace")
    return proc.returncode


def collect_cs_mtime(target_path: Path) -> Dict[str, float]:
    """收集目標路徑下所有 .cs / .csproj / .resx 的最後修改時間，供 watch 模式比對"""
    root = target_path if target_path.is_dir() else target_path.parent
    mtimes: Dict[str, float] = {}
    for ext in ("*.cs", "*.csproj", "*.resx"):
        for f in root.rglob(ext):
            if "obj" in f.parts:
                continue
            try:
                mtimes[str(f)] = f.stat().st_mtime
            except OSError:
                pass
    return mtimes


def watch_project(target_path: Path, eep_root: Optional[str], report_file: Optional[Path]) -> int:
    """監控專案檔案變更，當 Claude Code 或開發者儲存 .cs 檔時自動執行修復與編譯"""
    eepc = find_eepc_exe()
    if not eepc:
        print("[錯誤] 找不到 eepc.exe，請先確認 dist/EEPCompiler_Portable/eepc.exe 存在。")
        return 2

    print("=" * 68)
    print(f"[EEP 自動監控編譯模式] 正在監控: {target_path}")
    print("當 Claude Code 或您修改並儲存任何 .cs / .csproj 時，將自動觸發修復與編譯。")
    print("按 Ctrl+C 可結束監控。")
    print("=" * 68)

    last_state = collect_cs_mtime(target_path)
    trigger_build(eepc, target_path, eep_root, report_file)

    try:
        while True:
            time.sleep(0.8)
            current_state = collect_cs_mtime(target_path)
            if current_state != last_state:
                changed_files = [
                    Path(k).name
                    for k, v in current_state.items()
                    if k not in last_state or last_state[k] != v
                ]
                print(f"\n[偵測到檔案變更] {', '.join(changed_files[:5])} -> 重新執行自動修復與編譯...")
                last_state = current_state
                trigger_build(eepc, target_path, eep_root, report_file)
                # 因為自動修復可能更新了 .csproj，重新取樣時間戳記避免無限迴圈
                last_state = collect_cs_mtime(target_path)
    except KeyboardInterrupt:
        print("\n[已停止監控]")
        return 0


def trigger_build(eepc: Path, target_path: Path, eep_root: Optional[str], report_file: Optional[Path]) -> int:
    """執行單次 build 並可選將 Claude 報告寫入檔案"""
    args = [str(eepc), "build", str(target_path), "--auto-repair", "--claude"]
    if eep_root:
        args.extend(["--eep-root", eep_root])

    proc = subprocess.run(
        args,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    output = proc.stdout or ""
    print(output)

    if report_file:
        try:
            report_file.write_text(output, encoding="utf-8")
            print(f"[已更新 AI 診斷報告檔] {report_file}")
        except OSError as ex:
            print(f"[警告] 無法寫入報告檔: {ex}")

    return proc.returncode


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Infolight EEP (.NET Framework / VS2005) 可攜式編譯與自動修復 Python 工具"
    )
    parser.add_argument(
        "command",
        choices=["build", "repair", "check", "env", "watch", "gui"],
        help="執行的指令：build(修復並編譯), repair(僅修復規則2~4), check(語法檢查規則5), watch(自動監控編譯), gui(開啟視窗介面), env(環境檢測)",
    )
    parser.add_argument("path", nargs="?", default="", help="專案路徑 (.sln / .csproj / 資料夾)")
    parser.add_argument("--eep-root", default="", help="訊光 EEP 根目錄 (內含 EEPNetServer / EEPNetClient)")
    parser.add_argument("--engine", default="auto", choices=["auto", "msbuild", "direct"], help="編譯引擎模式")
    parser.add_argument("--target", default="auto", choices=["auto", "v2.0", "v4.0"], help="目標 CLR 版本")
    parser.add_argument("--claude", action="store_true", help="輸出 Claude Code 專用 Markdown 診斷報告")
    parser.add_argument(
        "--report-file",
        default="",
        help="在 watch 或 build 模式下，將 Claude 診斷報告同步寫入指定 Markdown 檔案",
    )

    args = parser.parse_args()
    eepc = find_eepc_exe()

    if args.command == "gui":
        if not eepc:
            print("[錯誤] 找不到 eepc.exe。")
            return 2
        subprocess.Popen([str(eepc), "--gui"])
        return 0

    if args.command == "env":
        if eepc:
            return run_via_eepc(eepc, ["env"])
        msb = find_msbuild_exe()
        print(f"MSBuild.exe: {msb if msb else '未找到'}")
        return 0

    if not args.path:
        print("[錯誤] 請提供專案路徑 (.sln / .csproj / 資料夾)。")
        return 2

    target = Path(args.path).resolve()
    if args.command == "watch":
        rep = Path(args.report_file).resolve() if args.report_file else (target.parent if target.is_file() else target) / "EEP_BUILD_STATUS.md"
        return watch_project(target, args.eep_root or None, rep)

    if eepc:
        forward_args = [args.command, str(target)]
        if args.eep_root:
            forward_args.extend(["--eep-root", args.eep_root])
        if args.engine != "auto":
            forward_args.extend(["--engine", args.engine])
        if args.target != "auto":
            forward_args.extend(["--target", args.target])
        if args.claude:
            forward_args.append("--claude")
        return run_via_eepc(eepc, forward_args)

    print("[錯誤] 找不到核心執行檔 eepc.exe。")
    return 2


if __name__ == "__main__":
    sys.exit(main())

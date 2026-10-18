請將我們剛才在對話中追查出的 EEP 舊系統商業邏輯、欄位規則、資料表關聯或除錯經驗，濃縮成結構化的知識條目，並追加（Append）至專案根目錄的 `EEP_KNOWLEDGE_BASE.md`（以及視需要更新 `.claude/skills/eep-maintainer/SKILL.md`）。

補充說明：$ARGUMENTS

寫入格式規範：
```markdown
### [模組代號 Cxxx / Sxxx] 畫面或功能名稱 (YYYY-MM-DD)
- **對應專案與檔案**: Client: `...`, Server: `...`
- **關聯資料表**: `...`
- **核心商業邏輯與維運筆記**:
  - 條列重點規則、狀態碼定義、計算公式與注意事項。
```

using System;
using System.ComponentModel;

namespace InfoRemoteModule
{
    /// <summary>
    /// 模擬訊光 EEP 遠端通訊基礎介面與模組
    /// </summary>
    public class RemoteModule : MarshalByRefObject
    {
        public string ModuleName { get; set; }
    }
}

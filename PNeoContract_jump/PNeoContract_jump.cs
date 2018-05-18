using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.Numerics;
using Helper = Neo.SmartContract.Framework.Helper;
using Neo.SmartContract.Framework.Services.System;

namespace PNeoContract_jump
{
    public class PNeoContract_jump : SmartContract
    { 
        static readonly byte[] superAdmin = Helper.ToScriptHash("Aeto8Loxsh7nXWoVS4FzBkUrFuiCB3Qidn");//初始管理員
        delegate object deleDyncall(string method, object[] arr);

        public static object Main(string method,object[] args)
        { 
            string magicstr = "for pneo test 003";

            //设置PNeo合约脚本地址
            if (method == "_setTarget")
            {
                if (Runtime.CheckWitness(superAdmin))
                {
                    Storage.Put(Storage.CurrentContext, "target", (byte[])args[0]);
                    return new byte[] { 0x01 };
                }
                return new byte[] { 0x00 };
            }
             
            var callscript = ExecutionEngine.CallingScriptHash;
            byte[] target = Storage.Get(Storage.CurrentContext, "target");

            if (method == "PNeoToWNeo")
            {
                object[] newarg = args;

                deleDyncall dyncall = (deleDyncall)target.ToDelegate();
                return dyncall(method, newarg); 
            }
             
            deleDyncall _dyncall = (deleDyncall)target.ToDelegate();
            return _dyncall(method, args);
        }
    }
}

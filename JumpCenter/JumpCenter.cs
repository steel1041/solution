using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.Numerics;

namespace JumpCenter
{
    public class JumpCenter : SmartContract
    {
        //wallet1
        static readonly byte[] SuperAdmin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        delegate object deleDyncall(string method, object[] arr);

        private const string TARGET_WNEO = "targetWneo";

        private const string TARGET_PNEO = "targetPneo";

       
        public static object Main(string method, object[] args)
        {
            string magicstr = "2018-05-21 11:47:00";
            if (method == "setTargetWneo")
            {
                if (Runtime.CheckWitness(SuperAdmin))
                {
                    Storage.Put(Storage.CurrentContext, TARGET_WNEO, (byte[])args[0]);
                    return new byte[] { 0x01 };
                }
                return new byte[] { 0x00 };
            }
            if (method == "setTargetPneo")
            {
                if (Runtime.CheckWitness(SuperAdmin))
                {

                    Storage.Put(Storage.CurrentContext, TARGET_PNEO, (byte[])args[0]);
                    return new byte[] { 0x01 };
                }
                return new byte[] { 0x00 };
            }

            var callscript = ExecutionEngine.CallingScriptHash;
            byte[] targetWneo = Storage.Get(Storage.CurrentContext, TARGET_WNEO);
            byte[] targetPneo = Storage.Get(Storage.CurrentContext, TARGET_PNEO);

            //调用P合约销毁相应数量
            if (method == "PNeoToWNeo")
            {
                object[] newarg = new object[2];
                newarg[0] = args[0];
                newarg[1] = args[1];
                deleDyncall dyncall = (deleDyncall)targetPneo.ToDelegate();
                return dyncall(method, newarg);
            }
            //调用W合约销毁相应数量
            if (method == "WNeoToPNeo")
            {
                object[] newarg = new object[2];
                newarg[0] = args[0];
                newarg[1] = args[1];
                deleDyncall dyncall = (deleDyncall)targetWneo.ToDelegate();
                return dyncall(method, newarg);
            }
          
            deleDyncall _dyncall = (deleDyncall)targetWneo.ToDelegate();
            return _dyncall(method, args);
        }
    }
}

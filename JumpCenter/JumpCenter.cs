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

        private const string TARGET_SDUSD = "targetSdusd";

       
        public static object Main(string method, object[] args)
        {
            string magicstr = "2018-05-23 11:47:00";
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
            if (method == "setTargetSDUSD")
            {
                if (Runtime.CheckWitness(SuperAdmin))
                {

                    Storage.Put(Storage.CurrentContext, TARGET_SDUSD, (byte[])args[0]);
                    return new byte[] { 0x01 };
                }
                return new byte[] { 0x00 };
            }
            //设置跳板调用合约地址
            if (method == "setCallScript")
            {
                if (args.Length != 2) return false;
                string type = (string)args[0];
                byte[] callScript = (byte[])args[1];

                //超级管理员设置跳板合约地址
                if (!Runtime.CheckWitness(SuperAdmin)) return false;
                return setCallScript(type,callScript);

            }

            var callscript = ExecutionEngine.CallingScriptHash;
            byte[] targetWneo = Storage.Get(Storage.CurrentContext, TARGET_WNEO);
            byte[] targetPneo = Storage.Get(Storage.CurrentContext, TARGET_PNEO);
            byte[] targetSdusd = Storage.Get(Storage.CurrentContext, TARGET_SDUSD);

            //P兑换W，P销毁功能,W发起
            if (method == "PNeoToWNeo")
            {
                //判断调用者是否是跳板合约
                byte[] jumpCallScript = getJumpCallScript("wneoCallScript");
                if (callscript.AsBigInteger() != jumpCallScript.AsBigInteger()) return false;

                object[] newarg = new object[3];
                newarg[0] = args[0];
                newarg[1] = args[1];
                newarg[2] = args[2];
                deleDyncall dyncall = (deleDyncall)targetPneo.ToDelegate();
                return dyncall(method, newarg);
            }
            //P合约查询
            if (method == "currentMountByP")
            {
                object[] newarg = new object[1];
                newarg[0] = args[0];
                deleDyncall dyncall = (deleDyncall)targetPneo.ToDelegate();
                return dyncall(method, newarg);
            }
            //P合约增发,SD发起
            if (method == "increase")
            {
                //判断调用者是否是跳板合约
                byte[] jumpCallScript = getJumpCallScript("sdusdCallScript");
                if (callscript.AsBigInteger() != jumpCallScript.AsBigInteger()) return false;

                object[] newarg = new object[3];
                newarg[0] = args[0];
                newarg[1] = args[1];
                newarg[2] = args[2];
                deleDyncall dyncall = (deleDyncall)targetPneo.ToDelegate();
                return dyncall(method, newarg);
            }
            //调用P合约销毁
            if (method == "destory")
            {
                //判断调用者是否是跳板合约
                byte[] jumpCallScript = getJumpCallScript("sdusdCallScript");
                if (callscript.AsBigInteger() != jumpCallScript.AsBigInteger()) return false;

                object[] newarg = new object[3];
                newarg[0] = args[0];
                newarg[1] = args[1];
                newarg[2] = args[2];
                deleDyncall dyncall = (deleDyncall)targetPneo.ToDelegate();
                return dyncall(method, newarg);
            }
            //调用W合约销毁相应数量
            if (method == "WNeoToPNeo")
            {
                //判断调用者是否是跳板合约
                byte[] jumpCallScript = getJumpCallScript("pneoCallScript");
                if (callscript.AsBigInteger() != jumpCallScript.AsBigInteger()) return false;
                object[] newarg = new object[3];
                newarg[0] = args[0];
                newarg[1] = args[1];
                newarg[2] = args[2];
                deleDyncall dyncall = (deleDyncall)targetWneo.ToDelegate();
                return dyncall(method, newarg);
            } 
            //调用W合约金额
            if (method == "currentMountByW")
            {
                object[] newarg = new object[1];
                newarg[0] = args[0];
                deleDyncall dyncall = (deleDyncall)targetWneo.ToDelegate();
                return dyncall(method, newarg);
            }

            //查询SD合约金额
            if (method == "currentMountBySD")
            {
                object[] newarg = new object[1];
                newarg[0] = args[0];
                deleDyncall dyncall = (deleDyncall)targetSdusd.ToDelegate();
                return dyncall(method, newarg);
            }

            deleDyncall _dyncall = (deleDyncall)targetWneo.ToDelegate();
            return _dyncall(method, args);
        }

        private static bool setCallScript(string type,byte[] callScript)
        {
            Storage.Put(Storage.CurrentContext, type, callScript);
            return true;
        }
        //sdusdCallScript、pneoCallScript、wneoCallScript
        private static byte[] getJumpCallScript(string type)
        {
            return Storage.Get(Storage.CurrentContext, type);
        }
    }
}

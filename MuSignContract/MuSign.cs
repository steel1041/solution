using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.ComponentModel;
using System.Numerics;

namespace MuSignContract
{
    public class MuSign : SmartContract
    {

        //超级管理员账户
        private static readonly byte[] admin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        //admin账户
        private const string ADMIN_ACCOUNT = "admin_account";

        private static byte[] getAccountKey(byte[] account) => new byte[] { 0x15 }.Concat(account);

        private static byte[] getAdminKey(byte[] key) => new byte[] { 0x16 }.Concat(key);


        public static Object Main(string operation, params object[] args)
        {
            var magicstr = "2018-11-19 17:40:10";

            if (Runtime.Trigger == TriggerType.Verification)//取钱才会涉及这里
            {
                return false;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                //必须在入口函数取得callscript，调用脚本的函数，也会导致执行栈变化，再取callscript就晚了
                var callscript = ExecutionEngine.CallingScriptHash;
                //this is in nep5

                if (operation == "setAccount")
                {
                    if (args.Length != 2) return false;
                    //操作地址，验证当前管理员账户
                    string key = (string)args[0];
                    byte[] address = (byte[])args[1];

                    if (!checkAdmin()) return false;
                    return setAccount(key, address);
                }
                if (operation == "setCallAccount")
                {
                    if (args.Length != 1) return false;
                    byte[] address = (byte[])args[0];

                    if (!checkAdmin()) return false;
                    return setCallAccount(address);
                }
                if (operation == "getCallAccount")
                {
                    if (args.Length != 1) return false;
                    byte[] address = (byte[])args[0];

                    return getCallAccount(address);
                }

            }
            return false;
        }

        public static bool setAccount(string key, byte[] address)
        {
            if (address.Length != 20)
                throw new InvalidOperationException("The parameters address and to SHOULD be 20-byte addresses.");

            Map<byte[], BigInteger> map = new Map<byte[], BigInteger>();

            Storage.Put(Storage.CurrentContext, getAdminKey(key.AsByteArray()), address);
            return true;
        }

        private static bool checkAdmin()
        {
            byte[] currAdmin = Storage.Get(Storage.CurrentContext, getAdminKey(ADMIN_ACCOUNT.AsByteArray()));
            if (currAdmin.Length > 0)
            {
                //当前地址和配置地址必须一致
                if (!Runtime.CheckWitness(currAdmin)) return false;
            }
            else
            {
                if (!Runtime.CheckWitness(admin)) return false;
            }
            return true;
        }

        public static bool setCallAccount(byte[] address)
        {
            if (address.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            //1:whitelist
            Storage.Put(Storage.CurrentContext, getAccountKey(address), 1);
            return true;
        }

        public static BigInteger getCallAccount(byte[] address)
        {
            if (address.Length != 20) return 0;

            return Storage.Get(Storage.CurrentContext, getAccountKey(address)).AsBigInteger();
        }

    }
}

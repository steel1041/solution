using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.ComponentModel;
using System.Numerics;


namespace SDUSD
{
    public class SDUSD : SmartContract
    {

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        //Default multiple signature committee account
        private static readonly byte[] committee = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        //Static param
        private const string TOTAL_SUPPLY = "totalSupply";
        private const string CALL_ACCOUNT = "call_account";
        private const string ADMIN_ACCOUNT = "admin_account";

        /*     
        * Key wrapper
        * map(address,balance)      key = 0x11+address
        * map(txid,TransferInfo)    key = 0x13+txid
        * map(str,address)          key = 0x15+str
        */
        private static byte[] getAccountKey(byte[] account) => new byte[] { 0x15 }.Concat(account);
        private static byte[] getBalanceKey(byte[] addr) => new byte[] { 0x11 }.Concat(addr);
        private static byte[] getTotalKey(byte[] total) => new byte[] { 0x12 }.Concat(total);

        /// <summary>
        ///   This smart contract is designed to implement NEP-5
        ///   Parameter List: 0710
        ///   Return List: 05
        /// </summary>
        /// <param name="operation">
        ///     The methos being invoked.
        /// </param>
        /// <param name="args">
        ///     Optional input parameters used by NEP5 methods.
        /// </param>
        /// <returns>
        ///     Return Object
        /// </returns>
        public static Object Main(string operation, params object[] args)
        {
            var magicstr = "2018-11-30 17:40:10";

            if (Runtime.Trigger == TriggerType.Verification)
            {
                return false;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                var callscript = ExecutionEngine.CallingScriptHash;
                //nep5 standard
                if (operation == "totalSupply") return totalSupply();
                if (operation == "name") return name();
                if (operation == "symbol") return symbol();
                if (operation == "decimals") return decimals();
                if (operation == "balanceOf")
                {
                    if (args.Length != 1) return 0;
                    byte[] account = (byte[])args[0];
                    return balanceOf(account);
                }
                if (operation == "transfer")
                {
                    if (args.Length != 3) return false;
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger amount = (BigInteger)args[2];

                    if (from.Length != 20 || to.Length != 20)
                        throw new InvalidOperationException("The parameters from and to SHOULD be 20-byte addresses.");

                    if (amount <= 0)
                        throw new InvalidOperationException("The parameter amount MUST be greater than 0.");

                    if (!IsPayable(to))
                        return false;

                    if (!Runtime.CheckWitness(from) && from.AsBigInteger() != callscript.AsBigInteger())
                        return false;
                    return transfer(from, to, amount);
                }
                if (operation == "setAccount")
                {
                    if (args.Length != 2) return false;
                    string key = (string)args[0];
                    byte[] address = (byte[])args[1];
                    //only committee account can call this method
                    if (!checkAdmin()) return false;

                    return setAccount(key, address);
                }
                if (operation == "getAccount")
                {
                    if (args.Length != 1) return false;
                    string key = (string)args[0];

                    return getAccount(key);
                }
                if (operation == "increase")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];

                    //only the legal SAR4C contract can call this method
                    if (getAccount(CALL_ACCOUNT).AsBigInteger() != callscript.AsBigInteger())
                        return false;
                    return increase(addr, value);
                }
                if (operation == "destory")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];

                    //only the legal SAR4C contract can call this method
                    if (getAccount(CALL_ACCOUNT).AsBigInteger() != callscript.AsBigInteger())
                        return false;
                    return destory(addr, value);
                }

                #region 升级合约,耗费490,仅限管理员
                if (operation == "upgrade")
                {
                    //only committee account can call this method
                    if (!checkAdmin()) return false;

                    if (args.Length != 1 && args.Length != 9)
                        return false;

                    byte[] script = Blockchain.GetContract(ExecutionEngine.ExecutingScriptHash).Script;
                    byte[] new_script = (byte[])args[0];

                    if (script == new_script)
                        return false;

                    byte[] parameter_list = new byte[] { 0x07, 0x10 };
                    byte return_type = 0x05;
                    bool need_storage = (bool)(object)05;
                    string name = "sdusd";
                    string version = "1";
                    string author = "alchemint";
                    string email = "0";
                    string description = "sdusd";

                    if (args.Length == 9)
                    {
                        parameter_list = (byte[])args[1];
                        return_type = (byte)args[2];
                        need_storage = (bool)args[3];
                        name = (string)args[4];
                        version = (string)args[5];
                        author = (string)args[6];
                        email = (string)args[7];
                        description = (string)args[8];
                    }
                    Contract.Migrate(new_script, parameter_list, return_type, need_storage, name, version, author, email, description);
                    return true;
                }
                #endregion

            }
            return false;
        }

        public static string name()
        {
            return "Standards USD";
        }
        public static string symbol()
        {
            return "SDUSD";
        }

        public static byte decimals()
        {
            return 8;
        }

        public static BigInteger totalSupply()
        {
            return Storage.Get(Storage.CurrentContext, getTotalKey(TOTAL_SUPPLY.AsByteArray())).AsBigInteger();
        }

        /*     
        * The committee account can set a new commitee account and set a legal SAR4C contract  
        */
        public static bool setAccount(string key, byte[] address)
        {
            if (address.Length != 20)
                throw new InvalidOperationException("The parameters address and to SHOULD be 20-byte addresses.");

            Storage.Put(Storage.CurrentContext, getAccountKey(key.AsByteArray()), address);
            return true;
        }

        private static bool checkAdmin()
        {
            byte[] currAdmin = Storage.Get(Storage.CurrentContext, getAccountKey(ADMIN_ACCOUNT.AsByteArray()));
            if (currAdmin.Length > 0)
            {
                if (!Runtime.CheckWitness(currAdmin)) return false;
            }
            else
            {
                if (!Runtime.CheckWitness(committee)) return false;
            }
            return true;
        }

        private static bool destory(byte[] addr, BigInteger value)
        {
            if (addr.Length != 20) return false;
            if (value <= 0) return false;

            if (transfer(addr, null, value))
            {
                BigInteger current = totalSupply();
                if (current - value < 0) return false;
                Storage.Put(Storage.CurrentContext, getTotalKey(TOTAL_SUPPLY.AsByteArray()), current - value);
                return true;
            }
            return false;
        }

        private static bool increase(byte[] addr, BigInteger value)
        {
            if (addr.Length != 20) return false;
            if (value <= 0) return false;

            if (transfer(null, addr, value))
            {
                BigInteger current = totalSupply();
                Storage.Put(Storage.CurrentContext, getTotalKey(TOTAL_SUPPLY.AsByteArray()), current + value);
                return true;
            }
            return false;
        }

        public static byte[] getAccount(string key)
        {

            return Storage.Get(Storage.CurrentContext, getAccountKey(key.AsByteArray()));
        }

        /// <summary>
        ///  Get the balance of the address
        /// </summary>
        /// <param name="address">
        ///  address
        /// </param>
        /// <returns>
        ///   account balance
        /// </returns>
        public static BigInteger balanceOf(byte[] address)
        {
            if (address.Length != 20) return 0;
            return Storage.Get(Storage.CurrentContext, getBalanceKey(address)).AsBigInteger();
        }

        /// <summary>
        ///   Transfer a token balance to another account.
        /// </summary>
        /// <param name="from">
        ///   The contract invoker.
        /// </param>
        /// <param name="to">
        ///   The account to transfer to.
        /// </param>
        /// <param name="value">
        ///   The amount to transfer.
        /// </param>
        /// <returns>
        ///   Transaction Successful?
        /// </returns>
        public static bool transfer(byte[] from, byte[] to, BigInteger value)
        {

            if (value <= 0) return false;

            if (from == to) return true;
            var fromKey = getBalanceKey(from);
            var toKey = getBalanceKey(to);

            if (from.Length > 0)
            {
                BigInteger from_value = Storage.Get(Storage.CurrentContext, fromKey).AsBigInteger();
                if (from_value < value) return false;
                if (from_value == value)
                    Storage.Delete(Storage.CurrentContext, fromKey);
                else
                    Storage.Put(Storage.CurrentContext, fromKey, from_value - value);
            }

            if (to.Length > 0)
            {
                BigInteger to_value = Storage.Get(Storage.CurrentContext, toKey).AsBigInteger();
                Storage.Put(Storage.CurrentContext, toKey, to_value + value);
            }

            //notify
            Transferred(from, to, value);
            return true;
        }

        private static bool IsPayable(byte[] to)
        {
            var c = Blockchain.GetContract(to); //0.1
            return c == null || c.IsPayable;
        }

    }
}
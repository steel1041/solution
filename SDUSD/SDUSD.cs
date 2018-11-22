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
        /*存储结构有     
        * map(address,balance)   存储地址余额   key = 0x11+address
        * map(txid,TransferInfo) 存储交易详情   key = 0x13+txid
        * map(str,address)      存储配置信息    key = 0x15+str
        */

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        //管理员账户
        private static readonly byte[] admin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        private const string TOTAL_SUPPLY = "totalSupply";

        //合约调用账户
        private const string CALL_ACCOUNT = "call_account";

        //admin账户
        private const string ADMIN_ACCOUNT = "admin_account";

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
            var magicstr = "2018-10-18 17:40:10";

            if (Runtime.Trigger == TriggerType.Verification)//取钱才会涉及这里
            {
                return false;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                //必须在入口函数取得callscript，调用脚本的函数，也会导致执行栈变化，再取callscript就晚了
                var callscript = ExecutionEngine.CallingScriptHash;
                //this is in nep5
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

                    //两种方式转账合并一起
                    if (!Runtime.CheckWitness(from) && from.AsBigInteger() != callscript.AsBigInteger())
                        return false;
                    return transfer(from, to, amount);
                }
                if (operation == "setAccount")
                {
                    if (args.Length != 2) return false;
                    //操作地址，验证当前管理员账户
                    string key = (string)args[0];
                    byte[] address = (byte[])args[1];

                    if (!checkAdmin()) return false;
                    return setAccount(key, address);
                }
                if (operation == "getAccount")
                {
                    if (args.Length != 1) return false;
                    string key = (string)args[0];

                    return getAccount(key);
                }
                //增发代币
                if (operation == "increase")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];

                    //判断调用者是否是授权合约
                    if (getAccount(CALL_ACCOUNT).AsBigInteger() != callscript.AsBigInteger())
                        return false;
                    return increase(addr, value);
                }
                //销毁代币
                if (operation == "destory")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];

                    //判断调用者是否是授权合约
                    if (getAccount(CALL_ACCOUNT).AsBigInteger() != callscript.AsBigInteger())
                        return false;
                    return destory(addr,value);
                }
                #region 升级合约,耗费490,仅限管理员
                if (operation == "upgrade")
                {
                    //不是管理员 不能操作
                    if (!checkAdmin()) return false;

                    if (args.Length != 1 && args.Length != 9)
                        return false;

                    byte[] script = Blockchain.GetContract(ExecutionEngine.ExecutingScriptHash).Script;
                    byte[] new_script = (byte[])args[0];
                    //如果传入的脚本一样 不继续操作
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

        //nep5 func
        public static BigInteger totalSupply()
        {
            return Storage.Get(Storage.CurrentContext, getTotalKey(TOTAL_SUPPLY.AsByteArray())).AsBigInteger();
        }

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
                //当前地址和配置地址必须一致
                if (!Runtime.CheckWitness(currAdmin)) return false;
            }
            else
            {
                if (!Runtime.CheckWitness(admin)) return false;
            }
            return true;
        }

        private static bool destory(byte[] addr, BigInteger value)
        {
            if (addr.Length != 20) return false;
            if (value <= 0) return false;
           
            if(transfer(addr, null, value)) { 
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

            if (transfer(null,addr,value))
            {
                BigInteger current = totalSupply();
                Storage.Put(Storage.CurrentContext, getTotalKey(TOTAL_SUPPLY.AsByteArray()), current + value);
                return true;
            }
            return false;
        }

        public static byte[] getAccount(string key) {

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
            //付款方
            if (from.Length > 0)
            {
                BigInteger from_value = Storage.Get(Storage.CurrentContext, fromKey).AsBigInteger();
                if (from_value < value) return false;
                if (from_value == value)
                    Storage.Delete(Storage.CurrentContext, fromKey);
                else
                    Storage.Put(Storage.CurrentContext, fromKey, from_value - value);
            }
            //收款方
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

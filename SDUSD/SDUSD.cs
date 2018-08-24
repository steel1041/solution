using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework.Services.System;

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

        //超级管理员账户
        private static readonly byte[] admin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        //因子
        private const ulong factor = 100000000;

        //总计数量
        private const ulong TOTAL_AMOUNT = 0;

        private const string TOTAL_SUPPLY = "totalSupply";

        private const string TOTAL_GENERATE = "totalGenerate";

        //合约收款账户
        private const string STORAGE_ACCOUNT = "storage_account";

        //SDS合约账户
        private const string SDS_ACCOUNT = "sds_account";

        //Oracle合约账户
        private const string ORACLE_ACCOUNT = "oracle_account";

        //WNEO合约账户
        private const string WASSET_ACCOUNT = "wasset_account";

        private const ulong SIX_POWER = 1000000;

        private static byte[] getAccountKey(byte[] account) => new byte[] { 0x15 }.Concat(account);

        private static byte[] getBalanceKey(byte[] addr) => new byte[] { 0x11 }.Concat(addr);

        private static byte[] getTxidKey(byte[] txid) => new byte[] { 0x13 }.Concat(txid);

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
            var magicstr = "2018-08-22 16:40:10";

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
                    if (from == to)
                        return true;
                    if (from.Length != 20 || to.Length != 20)
                        return false;
                    BigInteger value = (BigInteger)args[2];
                    //没有from签名，不让转
                    if (!Runtime.CheckWitness(from))
                        return false;
                    return transfer(from, to, value);
                }
                //允许合约调用
                if (operation == "transfer_contract")
                {
                    if (args.Length != 3) return false;
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    if (from.Length != 20 || to.Length != 20)
                        return false;
                    if (callscript.AsBigInteger() != from.AsBigInteger())
                        return false;
                    return transfer(from, to, value);
                }

                if (operation == "getTXInfo")
                {
                    if (args.Length != 1) return 0;
                    byte[] txid = (byte[])args[0];
                    return getTXInfo(txid);
                }
                if (operation == "setAccount")
                {
                    if (args.Length != 1) return false;
                    byte[] address = (byte[])args[0];

                    if (!Runtime.CheckWitness(admin)) return false;
                    return setAccount(address);
                }
                if (operation == "getAccount")
                {
                    if (args.Length != 1) return false;
                    byte[] address = (byte[])args[0];

                    return getAccount(address);
                }
                //增发代币
                if (operation == "increase")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    //判断调用者是否是授权合约
                    if (getAccount(callscript) != 1) return false;
                    return increase(addr, value);
                }
                //销毁代币
                if (operation == "destory")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    //判断调用者是否是授权合约
                    if (getAccount(callscript) != 1) return false;
                    return destory(addr,value);
                }

            }
            return false;
        }

        //nep5 func
        public static BigInteger totalSupply()
        {
            return Storage.Get(Storage.CurrentContext, getTotalKey(TOTAL_SUPPLY.AsByteArray())).AsBigInteger();
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



        public static bool setAccount(byte[] address)
        {
            if (address.Length != 20) return false;
            //1:whitelist
            Storage.Put(Storage.CurrentContext, getAccountKey(address), 1);
            return true;
        }

        public static BigInteger getAccount(byte[] address) {
            if (address.Length != 20) return 0;

            return Storage.Get(Storage.CurrentContext, getAccountKey(address)).AsBigInteger();
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
            //记录交易信息
            setTxInfo(from, to, value);
            //notify
            Transferred(from, to, value);
            return true;
        }


        private static BigInteger getConfig(string key)
        {
            if (key == null || key == "") return 0;
            return Storage.Get(Storage.CurrentContext, key.AsByteArray()).AsBigInteger();
        }

        private static Boolean setConfig(string key, BigInteger value)
        {
            if (key == null || key == "") return false;
            //只允许超管操作
            if (!Runtime.CheckWitness(admin)) return false;

            Storage.Put(Storage.CurrentContext, key.AsByteArray(), value);
            return true;
        }


        public static TransferInfo getTXInfo(byte[] txid)
        {
            byte[] v = Storage.Get(Storage.CurrentContext, getTxidKey(txid));
            if (v.Length == 0)
                return null;
            //新式实现方法只要一行
            return (TransferInfo)Helper.Deserialize(v);
        }

        private static void setTxInfo(byte[] from, byte[] to, BigInteger value)
        {
            TransferInfo info = new TransferInfo();
            info.from = from;
            info.to = to;
            info.value = value;
            byte[] txinfo = Helper.Serialize(info);

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            var keyTxid = getTxidKey(txid);
            Storage.Put(Storage.CurrentContext, keyTxid, txinfo);
        }

        public class TransferInfo
        {
            public byte[] from;
            public byte[] to;
            public BigInteger value;
        }

    }
}

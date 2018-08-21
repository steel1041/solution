using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework.Services.System;

namespace PNeoContract1
{
    public class PNeoContract : SmartContract
    {
        //NEO Asset
        private static readonly byte[] neo_asset_id = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        [DisplayName("approve")]
        public static event Action<byte[], byte[], BigInteger> Approved;
         
        [Appcall("95e6b39d3557f5ba5ba59fab178f6de3c24e3d04")] //JumpCenter ScriptHash
        public static extern object JumpCenterContract(string method, object[] args);

        //超级管理员账户
        private static readonly byte[] SuperAdmin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        private const string TOTAL_DESTORY = "totalDestory";


        //因子
        private const ulong factor = 100000000;

        //总计数量
        private const ulong TOTAL_AMOUNT = 0;

        private const string TOTAL_SUPPLY = "totalSupply";


        /*存储结构有     
          * map(address,balance)   存储地址余额   key = 0x11+address
          * map(txid,TransferInfo) 存储交易详情   key = 0x13+txid
         */
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
            //必须在入口函数取得callscript，调用脚本的函数，也会导致执行栈变化，再取callscript就晚了  
            var callscript = ExecutionEngine.CallingScriptHash;

            var magicstr = "2018-06-27 14:38:10";

            if (Runtime.Trigger == TriggerType.Verification)//取钱才会涉及这里
            {
                return false;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
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

                //WNeo换成PNeo(动态调用WNeo)
                if (operation == "WNeoToPNeo")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    //var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
                    object[] param = new object[3];
                    param[0] = addr;
                    param[1] = value;

                    if (!(bool)JumpCenterContract(operation, param)) return false;
                    return increaseBySelf(addr, value);
                }

                //P兑换成W，先销毁P
                if (operation == "PNeoToWNeo")
                {
                    if (args.Length != 3) return false;

                    byte[] addr = (byte[])args[0];
                    byte[] txid = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];

                    //判断调用者是否是跳板合约
                    byte[] jumpCallScript = getJumpCallScript();
                    if (callscript.AsBigInteger() != jumpCallScript.AsBigInteger()) return false;

                    return destoryByW(addr, txid, value);
                }

                //销毁代币，直接方法，风险极高
                if (operation == "destory")
                {
                    if (args.Length != 3) return false;
                    byte[] addr = (byte[])args[0];
                    byte[] txid = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    //判断调用者是否是跳板合约
                    byte[] jumpCallScript = getJumpCallScript();
                    if (callscript.AsBigInteger() != jumpCallScript.AsBigInteger()) return false;

                    return destoryBySD(addr, txid, value);
                }
                //增发代币，直接方法，风险极高
                if (operation == "increase")
                {
                    if (args.Length != 3) return false;
                    byte[] addr = (byte[])args[0];
                    byte[] txid = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    //判断调用者是否是跳板合约
                    byte[] jumpCallScript = Storage.Get(Storage.CurrentContext, "callScript");
                    if (callscript.AsBigInteger() != jumpCallScript.AsBigInteger()) return false;
                    return increaseBySD(addr, txid, value);
                }
                //设置跳板调用合约地址
                if (operation == "setCallScript")
                {
                    if (args.Length != 1) return false;
                    byte[] callScript = (byte[])args[0];

                    //超级管理员设置跳板合约地址
                    if (!Runtime.CheckWitness(SuperAdmin)) return false;
                    return setCallScript(callScript);
                }
                //计算总抵押数
                if (operation == "totalDestory")
                {
                    return totalDestory();
                }

            }
            return false;
        }

        //nep5 func
        public static BigInteger totalSupply()
        {
            return Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
        }
        public static string name()
        {
            return "P NEO";
        }
        public static string symbol()
        {
            return "PNEO";
        }

        public static byte decimals()
        {
            return 8;
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
            return Storage.Get(Storage.CurrentContext, new byte[] {0x11}.Concat(address)).AsBigInteger();
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
            var fromKey = new byte[] { 0x11 }.Concat(from);
            var toKey = new byte[] { 0x11 }.Concat(to);
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

        private static BigInteger totalDestory()
        {
            return Storage.Get(Storage.CurrentContext, TOTAL_DESTORY).AsBigInteger();
        }

        private static bool setCallScript(byte[] callScript)
        {
            Storage.Put(Storage.CurrentContext,"callScript",callScript);
            return true;
        }

        private static byte[] getJumpCallScript()
        {
            return Storage.Get(Storage.CurrentContext, "callScript");
        }

        private static BigInteger currentMountByP(byte[] txid)
        {
            return Storage.Get(Storage.CurrentContext, txid).AsBigInteger();
        }

        public static bool operateTotalSupply(BigInteger mount)
        {
            BigInteger current = Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
            if (current + mount >= 0)
            {
                Storage.Put(Storage.CurrentContext, TOTAL_SUPPLY, current + mount);
            }
            return true;
        }

        public static TransferInfo getTXInfo(byte[] txid)
        {
            byte[] v = Storage.Get(Storage.CurrentContext, new byte[] { 0x13 }.Concat(txid));
            if (v.Length == 0)
                return null;
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
            var keyTxid = new byte[] { 0x13 }.Concat(txid);
            Storage.Put(Storage.CurrentContext, keyTxid, txinfo);
        }

        public class TransferInfo
        {
            public byte[] from;
            public byte[] to;
            public BigInteger value;
        }

        private static byte[] byteLen(BigInteger n)
        {
            byte[] v = n.AsByteArray();
            if (v.Length > 2)
                throw new Exception("not support");
            if (v.Length < 2)
                v = v.Concat(new byte[1] { 0x00 });
            if (v.Length < 2)
                v = v.Concat(new byte[1] { 0x00 });
            return v;
        }
      

        //增发货币
        public static bool increaseBySD(byte[] to,byte[] txid,BigInteger value)
        {
            if (value <= 0) return false;

            transfer(null, to, value);

            BigInteger current = Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
            if (current + value >= 0)
            {
                Storage.Put(Storage.CurrentContext, TOTAL_SUPPLY, current + value);
            }
            return true;
        }
        //增发货币
        public static bool increaseBySelf(byte[] to,  BigInteger value)
        {
            if (value <= 0) return false;

            transfer(null, to, value);

            operateTotalSupply(value);
            return true;
        }

        //销毁货币
        public static bool destoryBySD(byte[] from, byte[] txid, BigInteger value)
        {
            if (value <= 0) return false;

            transfer(from, null, value);

            operateTotalSupply(0-value);

            //记录总抵押量
            operateTotalDestory(from, value);
            return true;
        }
        private static bool operateTotalDestory(byte[] from, BigInteger value)
        {
            BigInteger curr = Storage.Get(Storage.CurrentContext, TOTAL_DESTORY).AsBigInteger();
            Storage.Put(Storage.CurrentContext, TOTAL_DESTORY, curr + value);
            return true;
        }

        //销毁货币
        public static bool destoryByW(byte[] from, byte[] txid,BigInteger value)
        {
            if (value <= 0) return false;
            if (!Runtime.CheckWitness(from)) return false;

            transfer(from, null, value);
            operateTotalSupply(0 - value);
            return true;
        }

        private static byte[] IntToBytes(BigInteger value)
        {
            byte[] buffer = value.ToByteArray();
            return buffer;
        }
        
        private static BigInteger BytesToInt(byte[] array)
        {
            var buffer = new BigInteger(array);
            return buffer;
        }

    }
}

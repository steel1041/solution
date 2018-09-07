using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework.Services.System;

namespace SNeoContract
{
    public class SNeo : SmartContract
    {
        /*存储结构有     
        * map(address,balance)   存储地址余额   key = 0x11+address
        * map(txid,TransferInfo) 存储交易详情   key = 0x13+txid
        * map(str,address)       存储合约地址   key = 0x14+str
        * map(coinid,address)    存储赎回信息   key = txid+ new byte[]{0,0}
       */
        //NEO Asset
        private static readonly byte[] neo_asset_id = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };

        //GAS Asset
        private static readonly byte[] gas_asset_id = { 231, 45, 40, 105, 121, 238, 108, 177, 183, 230, 93, 253, 223, 178, 227, 132, 16, 11, 141, 20, 142, 119, 88, 222, 66, 228, 22, 139, 113, 121, 44, 96 };

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        [DisplayName("operator4C")]
        public static event Action<byte[],byte[],BigInteger,BigInteger> Operator4C;

        public delegate object NEP5Contract(string method, object[] args);


        //配置参数-NEO市场价格
        private const string CONFIG_PRICE_NEO = "neo_price";

        //配置参数-GAS市场价格
        private const string CONFIG_PRICE_GAS = "gas_price";

        //合约收款账户
        private const string STORAGE_ACCOUNT = "storage_account";

        //SDS合约账户
        private const string SDS_ACCOUNT = "sds_account";

        //Oracle合约账户
        private const string ORACLE_ACCOUNT = "oracle_account";

        //管理员账户
        private static readonly byte[] admin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7"); 

        //因子
        private const ulong factor = 100000000;

        //总计数量
        private const ulong TOTAL_AMOUNT = 0;

        private const string TOTAL_SUPPLY = "totalSupply";

        private const string TOTAL_DESTORY = "totalDestory";

        private static byte[] getBalanceKey(byte[] addr) => new byte[] { 0x11 }.Concat(addr);

        private static byte[] getTotalKey(byte[] total) => new byte[] { 0x12 }.Concat(total);

        private static byte[] getTxidKey(byte[] txid) => new byte[] { 0x13 }.Concat(txid);

        private static byte[] getAccountKey(byte[] account) => new byte[] { 0x14 }.Concat(account);

        //交易类型
        public enum ConfigTranType
        {
            TRANSACTION_TYPE_MINT = 1,//兑换SNEO
            TRANSACTION_TYPE_REFUND,   //赎回NEO
            TRANSACTION_TYPE_MINT_GAS,//兑换SGAS
            TRANSACTION_TYPE_REFUND_GAS   //赎回GAS
        }

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
            var magicstr = "2018-08-30 15:04:10";

            if (Runtime.Trigger == TriggerType.Verification)//取钱才会涉及这里
            {
                var tx = (Transaction)ExecutionEngine.ScriptContainer;
                var curhash = ExecutionEngine.ExecutingScriptHash;
                var inputs = tx.GetInputs();
                var outputs = tx.GetOutputs();

                //检查输入是不是有被标记过
                for (var i = 0; i < inputs.Length; i++)
                {
                    byte[] coinid = inputs[i].PrevHash.Concat(new byte[] { 0, 0 });
                    if (inputs[i].PrevIndex == 0)//如果utxo n为0 的话，是有可能是一个标记utxo的
                    {
                        byte[] target = Storage.Get(Storage.CurrentContext, coinid);
                        if (target.Length > 0)
                        {
                            if (inputs.Length > 1 || outputs.Length != 1)//使用标记coin的时候只允许一个输入\一个输出
                                return false;

                            //如果只有一个输入，一个输出，并且目的转账地址就是授权地址
                            //允许转账
                            if (outputs[0].ScriptHash.AsBigInteger() == target.AsBigInteger())
                                return true;
                            else//否则不允许
                                return false;
                        }
                    }
                }
                //走到这里没跳出，说明输入都没有被标记
                var refs = tx.GetReferences();
                BigInteger inputcount = 0;
                for (var i = 0; i < refs.Length; i++)
                {
                    if (refs[i].AssetId.AsBigInteger() != neo_asset_id.AsBigInteger())
                        return false;//不允许操作除gas以外的

                    if (refs[i].ScriptHash.AsBigInteger() != curhash.AsBigInteger())
                        return false;//不允许混入其它地址

                    inputcount += refs[i].Value;
                }
                //检查有没有钱离开本合约
                BigInteger outputcount = 0;
                for (var i = 0; i < outputs.Length; i++)
                {
                    if (outputs[i].ScriptHash.AsBigInteger() != curhash.AsBigInteger())
                    {
                        return false;
                    }
                    outputcount += outputs[i].Value;
                }
                if (outputcount != inputcount)
                    return false;
                //没有资金离开本合约地址，允许
                return true;
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
                //退款
                if (operation == "refund")
                {
                    if (args.Length != 1) return 0;
                    byte[] who = (byte[])args[0];
                    if (!Runtime.CheckWitness(who))
                        return false;
                    return refund(who);
                }
                if (operation == "getRefundTarget")
                {
                    if (args.Length != 1) return 0;
                    byte[] txid = (byte[])args[0];
                    return getRefundTarget(txid);
                }
                //设置全局参数
                if (operation == "setConfig")
                {
                    if (args.Length != 2) return false;
                    string key = (string)args[0];
                    BigInteger value = (BigInteger)args[1];
                    return setConfig(key, value);
                }
                //查询全局参数
                if (operation == "getConfig")
                {
                    if (args.Length != 1) return false;
                    string key = (string)args[0];
                    return getConfig(key);
                }
                if (operation == "setAccount")
                {
                    if (args.Length != 2) return false;
                    string key = (string)args[0];
                    byte[] address = (byte[])args[1];
                    if (!Runtime.CheckWitness(admin)) return false;

                    return setAccount(key, address);
                }
                if (operation == "mintTokens")
                {
                    if (args.Length != 1) return 0;
                    string type = (string)args[0];

                    byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
                    return mintTokens(oracleAssetID,type);
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
            return "SNEO";
        }
        public static string symbol()
        {
            return "SNEO";
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
            //setTxInfo(from, to, value);
            //notify
            Transferred(from, to, value);
            return true;
        }

        //退款
        public static bool refund(byte[] who)
        {
            var tx = (Transaction)ExecutionEngine.ScriptContainer;
            var outputs = tx.GetOutputs();
            //退的不是neo，不行
            if (outputs[0].AssetId.AsBigInteger() != neo_asset_id.AsBigInteger())
                return false;
            //不是转给自身，不行
            if (outputs[0].ScriptHash.AsBigInteger() != ExecutionEngine.ExecutingScriptHash.AsBigInteger())
                return false;

            //当前的交易已经名花有主了，不行
            byte[] target = getRefundTarget(tx.Hash);
            if (target.Length > 0)
                return false;

            //尝试销毁一定数量的代币
            var count = outputs[0].Value;
            bool b = transfer(who, null, count);
            if (!b)
                return false;

            //标记这个utxo归我所有
            byte[] coinid = tx.Hash.Concat(new byte[] { 0, 0 });
            Storage.Put(Storage.CurrentContext,coinid, who);

            //改变总量
            operateTotalSupply(0-count);

            //记录操作日志
            Operator4C(tx.Hash,who,count,(int)ConfigTranType.TRANSACTION_TYPE_REFUND);

            //Header header = Blockchain.GetHeader(Blockchain.GetHeight());
            //header.Timestamp;
            return true;
        }

        public static byte[] getRefundTarget(byte[] txid)
        {
            byte[] coinid = txid.Concat(new byte[] { 0, 0 });
            byte[] target = Storage.Get(Storage.CurrentContext, coinid);
            return target;
        }

        public static TransferInfo getTXInfo(byte[] txid)
        {
            byte[] v = Storage.Get(Storage.CurrentContext, getTxidKey(txid));
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
            var keyTxid = getTxidKey(txid);
            Storage.Put(Storage.CurrentContext, keyTxid, txinfo);
        }

        public static bool mintTokens(byte[] oracleAssetID,string type)
        {
            var tx = (Transaction)ExecutionEngine.ScriptContainer;
             
            //默认是neo资产   neo,gas
            byte[] asset_id = neo_asset_id;
            if (type == "gas") {
                asset_id = gas_asset_id;
            }
            //获取投资人，谁要换neo or gas
            byte[] who = null;
            TransactionOutput[] reference = tx.GetReferences();
            for (var i = 0; i < reference.Length; i++)
            {
                if (reference[i].AssetId.AsBigInteger() == asset_id.AsBigInteger())
                {
                    who = reference[i].ScriptHash;
                    break;
                }
            }

            TransactionOutput[] outputs = tx.GetOutputs();
            ulong value = 0;
            // get the total amount of Neo
            // 获取转入智能合约地址的NEO总量
            foreach (TransactionOutput output in outputs)
            {
                if (output.ScriptHash == ExecutionEngine.ExecutingScriptHash &&
                    output.AssetId.AsBigInteger() == asset_id.AsBigInteger())
                {
                    value += (ulong)output.Value;
                }
            }
            //获取实际兑换量
            BigInteger realValue = getRealValue(oracleAssetID,type, value);

            if (transfer(null, who, realValue)) {
                operateTotalSupply(realValue);
                //记录操作日志
                Operator4C(tx.Hash, who, realValue, (int)ConfigTranType.TRANSACTION_TYPE_MINT);
                return true;
            }
            
            return false;

        }

        private static BigInteger getRealValue(byte[] oracleAssetID,string type, ulong value)
        {
            if (value <= 0) return 0;
            if (type == "gas") {
                BigInteger neoPrice = 0; 
                {
                    var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                    object[] arg = new object[1];
                    arg[0] = CONFIG_PRICE_NEO;
                    BigInteger re = (BigInteger)OracleContract("getPrice", arg);
                    if (re != 0)
                        neoPrice = re;
                }

                BigInteger gasPrice = 0;
                {
                    var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                    object[] arg = new object[1];
                    arg[0] = CONFIG_PRICE_GAS;
                    BigInteger re = (BigInteger)OracleContract("getPrice", arg);
                    if (re != 0)
                        gasPrice = re;
                }
                if (neoPrice == 0 || gasPrice == 0) {
                    return value * 2/10;
                }
                return value * gasPrice/neoPrice;
            }
            return value;
        }

        public static bool operateTotalSupply(BigInteger mount)
        {
            BigInteger current = totalSupply();
            if (current + mount >= 0)
            {
                Storage.Put(Storage.CurrentContext, getTotalKey(TOTAL_SUPPLY.AsByteArray()), current + mount);
            }
            return true;
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

  
        private static BigInteger getConfig(string key)
        {
            if (key == null || key == "") return 0;
            return Storage.Get(Storage.CurrentContext, key.AsByteArray()).AsBigInteger();
        }

        private static Boolean setConfig(string key, BigInteger value)
        {
            if (key == null || key == "") return false;
            if (!Runtime.CheckWitness(admin)) return false;
            Storage.Put(Storage.CurrentContext, key.AsByteArray(), value);
            return true;
        }

        public static bool setAccount(string key, byte[] address)
        {
            if (key == null || key == "") return false;

            if (address.Length != 20) return false;
            Storage.Put(Storage.CurrentContext, getAccountKey(key.AsByteArray()), address);

            return true;
        }

    }
}

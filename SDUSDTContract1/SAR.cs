using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework.Services.System;

namespace SDUSDTContract1
{
    public class SAR : SmartContract
    {
        /*存储结构有     
        * map(address,balance)   存储地址余额   key = 0x11+address
        * map(txid,TransferInfo) 存储交易详情   key = 0x13+txid
        * map(address,SAR)   存储SAR信息        key = 0x12+address
        * map(txid,SARDetail)存储SAR详细信息    key = 0x14+txid
        * map(str,address)      存储配置信息    key = 0x15+str
        */

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        /* addr,sartxid,txid,type,operated*/
        [DisplayName("sarOperator4C")]
        public static event Action<byte[], byte[], byte[], BigInteger, BigInteger> Operated;

        public delegate object NEP5Contract(string method, object[] args);

        //超级管理员账户
        private static readonly byte[] admin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        [Appcall("95e6b39d3557f5ba5ba59fab178f6de3c24e3d04")] //JumpCenter ScriptHash
        public static extern object JumpCenterContract(string method, object[] args);

        //因子
        private const ulong factor = 100000000;

        //总计数量
        private const ulong TOTAL_AMOUNT = 0;

        private const string TOTAL_SUPPLY = "totalSupply";

        private const string TOTAL_GENERATE = "totalGenerate";

        //配置参数-兑换比率，百分位，如150、200
        private const string CONFIG_RATE = "neo_rate";

        //配置参数-NEO市场价格
        private const string CONFIG_PRICE_NEO = "neo_price";

        //配置参数-GAS市场价格
        private const string CONFIG_PRICE_GAS = "gas_price";

        //配置参数-清算比率，百分位，如110
        private const string CONFIG_CLEAR_RATE = "clear_rate";

        //最低抵押率
        private const string CONFIG_RATE_C = "liquidate_rate_c";

        //合约收款账户
        private const string STORAGE_ACCOUNT = "storage_account";

        //SDS合约账户
        private const string SDS_ACCOUNT = "sds_account";

        //Oracle合约账户
        private const string ORACLE_ACCOUNT = "oracle_account";

        //WNEO合约账户
        private const string WASSET_ACCOUNT = "wasset_account";

        //SDUSD合约账户
        private const string SDUSD_ACCOUNT = "sdusd_account";

        private const ulong SIX_POWER = 1000000;

        private static byte[] getSARKey(byte[] addr) => new byte[] { 0x12 }.Concat(addr);

        private static byte[] getTxidKey(byte[] txid) => new byte[] { 0x14 }.Concat(txid);

        private static byte[] getAccountKey(byte[] account) => new byte[] { 0x15 }.Concat(account);


        //交易类型
        public enum ConfigTranType {
            TRANSACTION_TYPE_OPEN=1,//创建SAR
            TRANSACTION_TYPE_LOCK,//锁仓
            TRANSACTION_TYPE_DRAW,//提取
            TRANSACTION_TYPE_FREE,//释放
            TRANSACTION_TYPE_WIPE,//赎回
            TRANSACTION_TYPE_SHUT,//关闭
            TRANSACTION_TYPE_FORCESHUT,//对手关闭
            TRANSACTION_TYPE_GIVE,//转移所有权
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
            var magicstr = "2018-06-05 16:40:10";

            if (Runtime.Trigger == TriggerType.Verification)//取钱才会涉及这里
            {
                return false;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                //必须在入口函数取得callscript，调用脚本的函数，也会导致执行栈变化，再取callscript就晚了
                var callscript = ExecutionEngine.CallingScriptHash;
                //this is in nep5
                //if (operation == "totalSupply") return totalSupply();
                //if (operation == "name") return name();
                //if (operation == "symbol") return symbol();
                //if (operation == "decimals") return decimals();
                //if (operation == "balanceOf")
                //{
                //    if (args.Length != 1) return 0;
                //    byte[] account = (byte[])args[0];
                //    return balanceOf(account);
                //}
                //if (operation == "transfer")
                //{
                //    if (args.Length != 3) return false;
                //    byte[] from = (byte[])args[0];
                //    byte[] to = (byte[])args[1];
                //    if (from == to)
                //        return true;
                //    if (from.Length != 20 || to.Length != 20)
                //        return false;
                //    BigInteger value = (BigInteger)args[2];
                //    //没有from签名，不让转
                //    if (!Runtime.CheckWitness(from))
                //        return false;
                //    return transfer(from, to, value);
                //}
                ////允许合约调用
                //if (operation == "transfer_contract")
                //{
                //    if (args.Length != 3) return false;
                //    byte[] from = (byte[])args[0];
                //    byte[] to = (byte[])args[1];
                //    BigInteger value = (BigInteger)args[2];
                //    if (from.Length != 20 || to.Length != 20)
                //        return false;
                //    if (callscript.AsBigInteger() != from.AsBigInteger())
                //        return false;
                //    return transfer(from, to, value);
                //}
              
                //if (operation == "getTXInfo")
                //{
                //    if (args.Length != 1) return 0;
                //    byte[] txid = (byte[])args[0];
                //    return getTXInfo(txid);
                //}
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
                //创建SAR记录
                if (operation == "openSAR4C")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return openSAR4C(addr);
                }
                //查询债仓记录
                if (operation == "getSAR4C")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];

                    return getSAR4C(addr);
                }
                //查询债仓详细操作记录
                if (operation == "getSARTxInfo")
                {
                    if (args.Length != 1) return false;
                    byte[] txid = (byte[])args[0];
                    return getSARTxInfo(txid);
                }
                //锁仓PNeo
                if (operation == "reserve")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    byte[] wAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(WASSET_ACCOUNT.AsByteArray()));
                    return reserve(wAssetID,addr, mount);
                }
                //提取SDUSD
                if (operation == "expande")
                {
                    if (args.Length != 2) return false;

                    byte[] addr = (byte[])args[0];
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
                    byte[] sdusdAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDUSD_ACCOUNT.AsByteArray()));

                    return expande(oracleAssetID, sdusdAssetID,addr, mount);
                }
                //释放未被兑换的PNEO
                if (operation == "withdraw")
                {
                    if (args.Length != 2) return false;

                    byte[] addr = (byte[])args[0];
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    byte[] wAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(WASSET_ACCOUNT.AsByteArray()));
                    byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
                    return withdraw(oracleAssetID,wAssetID,addr, mount);
                }
                //赎回质押的PNEO，用SDUSD去兑换
                if (operation == "contract")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;
                    byte[] sdusdAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDUSD_ACCOUNT.AsByteArray()));

                    return contract(sdusdAssetID,addr, mount);
                }
                //关闭在仓
                if (operation == "close")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];

                    if (!Runtime.CheckWitness(addr)) return false;
                    byte[] wAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(WASSET_ACCOUNT.AsByteArray()));
                    byte[] sdusdAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDUSD_ACCOUNT.AsByteArray()));
                    return close(wAssetID,sdusdAssetID,addr);
                }
                //强制关闭在仓，由别人发起
                if (operation == "bite")
                {
                    if (args.Length != 2) return false;
                    byte[] otherAddr = (byte[])args[0];
                    byte[] addr = (byte[])args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    byte[] wAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(WASSET_ACCOUNT.AsByteArray()));
                    byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
                    byte[] sdusdAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDUSD_ACCOUNT.AsByteArray()));
                    return bite(oracleAssetID,wAssetID,sdusdAssetID,otherAddr, addr);
                }
                //可赎回金额
                if (operation == "balanceOfRedeem")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];
                    return balanceOfRedeem(addr);
                }
                //赎回剩余PNEO
                if (operation == "redeem")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];

                    if (!Runtime.CheckWitness(addr)) return false;

                    byte[] wAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(WASSET_ACCOUNT.AsByteArray()));
                    return redeem(wAssetID,addr);
                }
                //转移SAR所有权给其它地址
                if (operation == "give")
                {
                    if (args.Length != 2) return false;
                    byte[] fromAdd = (byte[])args[0];
                    byte[] toAdd = (byte[])args[1];

                    if (!Runtime.CheckWitness(fromAdd)) return false;
                    return give(fromAdd, toAdd);
                }
                //计算总生成数量
                //if (operation == "totalGenerate")
                //{
                //    return totalGenerate();
                //}
                if (operation == "setAccount")
                {
                    if (args.Length != 2) return false;
                    string key = (string)args[0];
                    byte[] address = (byte[])args[1];
                    if (!Runtime.CheckWitness(admin)) return false;

                    return setAccount(key, address);
                }
            }
            return false;
        }

        //nep5 func
        //public static BigInteger totalSupply()
        //{
        //    return Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
        //}
        //public static string name()
        //{
        //    return "Special Drawing USD";
        //}
        //public static string symbol()
        //{
        //    return "SDUSD";
        //}

        //public static byte decimals()
        //{
        //    return 8;
        //}

        public static bool setAccount(string key, byte[] address)
        {
            if (key == null || key == "") return false;

            if (address.Length != 20) return false;
            Storage.Put(Storage.CurrentContext, getAccountKey(key.AsByteArray()), address);

            return true;
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
        //public static BigInteger balanceOf(byte[] address)
        //{
        //    if (address.Length != 20) return 0;
        //    return Storage.Get(Storage.CurrentContext, new byte[] { 0x11 }.Concat(address)).AsBigInteger();
        //}

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
        //public static bool transfer(byte[] from, byte[] to, BigInteger value)
        //{

        //    if (value <= 0) return false;

        //    if (from == to) return true;
        //    var fromKey = new byte[] { 0x11 }.Concat(from);
        //    var toKey = new byte[] { 0x11 }.Concat(to);
        //    //付款方
        //    if (from.Length > 0)
        //    {
        //        BigInteger from_value = Storage.Get(Storage.CurrentContext, fromKey).AsBigInteger();
        //        if (from_value < value) return false;
        //        if (from_value == value)
        //            Storage.Delete(Storage.CurrentContext, fromKey);
        //        else
        //            Storage.Put(Storage.CurrentContext, fromKey, from_value - value);
        //    }
        //    //收款方
        //    if (to.Length > 0)
        //    {
        //        BigInteger to_value = Storage.Get(Storage.CurrentContext, toKey).AsBigInteger();
        //        Storage.Put(Storage.CurrentContext, toKey, to_value + value);
        //    }
        //    //记录交易信息
        //    setTxInfo(from, to, value);
        //    //notify
        //    Transferred(from, to, value);
        //    return true;
        //}

        //private static BigInteger totalGenerate()
        //{
        //    return Storage.Get(Storage.CurrentContext, TOTAL_GENERATE).AsBigInteger();
        //}


        //public static bool operateTotalSupply(BigInteger mount)
        //{
        //    BigInteger current = Storage.Get(Storage.CurrentContext, new byte[] { 0x15}.Concat(TOTAL_SUPPLY.AsByteArray())).AsBigInteger();
        //    if (current + mount >= 0) {
        //        Storage.Put(Storage.CurrentContext, new byte[] { 0x15 }.Concat(TOTAL_SUPPLY.AsByteArray()), current + mount);
        //    }
        //    return true;
        //}

        private static BigInteger currentMount(byte[] txid)
        {
           return Storage.Get(Storage.CurrentContext, txid).AsBigInteger();
         }

        private static BigInteger balanceOfRedeem(byte[] addr)
        {
            //被清仓用户剩余PNEO所得
            var otherKey = addr.Concat(ConvertN(1));
            BigInteger currentRemain = Storage.Get(Storage.CurrentContext, otherKey).AsBigInteger();
            if (currentRemain <= 0)
            {
                return 0;
            }
            return currentRemain;
        }


        private static bool redeem(byte[] wAssetID,byte[] addr)
        {

            //被清仓用户剩余PNEO所得
            var otherKey = addr.Concat(ConvertN(1));
            BigInteger currentRemain = Storage.Get(Storage.CurrentContext, otherKey).AsBigInteger();
            if (currentRemain <= 0)
            {
                return false;
            }
            //保存剩余PNEO金额
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            byte[] from = Storage.Get(Storage.CurrentContext, new byte[] { 0x15 }.Concat(STORAGE_ACCOUNT.AsByteArray()));
            if (from.Length == 0) return false;
            {
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = addr;
                arg[2] = currentRemain;
                var WContract = (NEP5Contract)wAssetID.ToDelegate();

                if (!(bool)WContract("transfer_contract", arg)) return false;
            }

            Storage.Delete(Storage.CurrentContext,otherKey);
            return true;
        }

        private static SARTransferDetail getSARTxInfo(byte[] txid)
        {
            byte[] v = Storage.Get(Storage.CurrentContext, txid);
            if (v.Length == 0)
                return null;
            return (SARTransferDetail)Helper.Deserialize(v);
        }

        private static SARInfo getSAR4C(byte[] addr)
        {
            //SAR是否存在
            byte[] key = getSARKey(addr);
            
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0)
                return null;
            SARInfo sarInfo = (SARInfo)Helper.Deserialize(sar);
            return sarInfo;
        }

        private static Boolean bite(byte[] oracleAssetID,byte[] wAssetID,byte[] sdusdAssetID,byte[] otherAddr, byte[] addr)
        {
            //SAR是否存在
            byte[] key = getSARKey(otherAddr);

            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0)
                return false;
            SARInfo sarInfo = (SARInfo)Helper.Deserialize(sar);

            BigInteger lockedPneo = sarInfo.locked;
            BigInteger hasDrawed = sarInfo.hasDrawed;

            //不需要清算
            if (hasDrawed <= 0) return false;

            //余额SDUSD是否足够
            BigInteger myBalance = 0;
            {
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = addr;
                myBalance = (BigInteger)SDUSDContract("balanceOf", arg);
            }

            if (hasDrawed > myBalance) return false;

            //当前NEO美元价格，需要从价格中心获取
            BigInteger neoPrice = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = CONFIG_PRICE_NEO;
                neoPrice = (BigInteger)OracleContract("getPrice", arg);
            }

            //当前兑换率，需要从配置中心获取
            BigInteger rate = getConfig(CONFIG_RATE);
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = CONFIG_RATE_C;
                rate = (BigInteger)OracleContract("getConfig", arg);
            }

            //当前清算折扣比例，需要从配置中心获取
            BigInteger rateClear = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = CONFIG_CLEAR_RATE;
                rate = (BigInteger)OracleContract("getConfig", arg);
            }

            //计算可以拿到的W资产
            BigInteger canClearPneo = hasDrawed * rateClear * SIX_POWER/ neoPrice;

            //剩余的PNEO记录到原用户账户下
            BigInteger remain = lockedPneo - canClearPneo;
            if (remain < 0) return false;

            //销毁等量SDUSD
            {
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[2];
                arg[0] = addr;
                arg[1] = hasDrawed;
                if(!(bool)SDUSDContract("destory", arg)) return false;
            }

            //总量处理
            BigInteger current = Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
            if (current - hasDrawed >= 0)
            {
                Storage.Put(Storage.CurrentContext, TOTAL_SUPPLY, current - hasDrawed);
            }

            //拿到该有的PNEO
            //保存剩余PNEO金额
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            byte[] from = Storage.Get(Storage.CurrentContext, new byte[] { 0x15 }.Concat(STORAGE_ACCOUNT.AsByteArray()));
            if (from.Length == 0) return false;
            {
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = addr;
                arg[2] = canClearPneo;
                var WContract = (NEP5Contract)wAssetID.ToDelegate();

                if (!(bool)WContract("transfer_contract", arg)) return false;
            }

            //删除CDP
            Storage.Delete(Storage.CurrentContext, key);

            if (remain > 0) {
                //被清仓用户剩余PNEO所得
                var otherKey = otherAddr.Concat(ConvertN(1));
                BigInteger currentRemain = Storage.Get(Storage.CurrentContext,otherKey).AsBigInteger();
                Storage.Put(Storage.CurrentContext, otherKey, currentRemain + remain);
            }
           
            //记录交易详细数据
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = sarInfo.txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_FORCESHUT;
            detail.operated = hasDrawed;
            detail.hasLocked = lockedPneo;
            detail.hasDrawed = hasDrawed;
            detail.txid = txid;
            Storage.Put(Storage.CurrentContext, new byte[] { 0x14 }.Concat(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_FORCESHUT, 0);
            return true;
        }

        private static BigInteger getConfig(string key)
        {
            if (key == null || key == "") return 0;
            return  Storage.Get(Storage.CurrentContext,key.AsByteArray()).AsBigInteger();
        }

        private static Boolean setConfig(string key, BigInteger value)
        {
            if (key == null || key == "") return false;
            //只允许超管操作
            if (!Runtime.CheckWitness(admin)) return false;

            Storage.Put(Storage.CurrentContext,key.AsByteArray(),value);
            return true;
        }

        private static Boolean contract(byte[] sdusdAssetID,byte[] addr, BigInteger mount)
        {
            if (addr.Length != 20) return false;
            if (mount <= 0) return false;

            //SAR是否存在
            byte[] key = getSARKey(addr);

            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0)
                return false;
            SARInfo sarInfo = (SARInfo)Helper.Deserialize(sar);

            BigInteger locked = sarInfo.locked;
            BigInteger hasDrawed = sarInfo.hasDrawed;

            //SD赎回量要小于已经在仓的
            if (mount > hasDrawed) return false;

            //减少金额
            {
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[2];
                arg[0] = addr;
                arg[1] = mount;
                if (!(bool)SDUSDContract("destory", arg)) return false;
            }
            //减少总量
            //operateTotalSupply(0 - mount);

            sarInfo.hasDrawed = hasDrawed - mount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            //记录交易详细数据
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = sarInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_WIPE;
            detail.operated = mount;
            detail.hasLocked = locked;
            detail.hasDrawed = hasDrawed;

            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_WIPE, mount);
            return true;
        }

        /// <summary>
        ///   This method is withdraw pneo asset.
        /// </summary>
        /// <param name="addr">
        ///     The address being invoked.
        /// </param>
        /// <param name="mount">
        ///     The mount need free,it's pneo.
        /// </param>
        /// <returns>
        ///     Return Boolean
        /// </returns>
        private static Boolean withdraw(byte[] oracleAssetID,byte[] wAssetID,byte[] addr, BigInteger mount)
        {
            if (mount <= 0) return false;

            //CDP是否存在
            byte[] key = getSARKey(addr);

            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0)
                return false;
            SARInfo sarInfo = (SARInfo)Helper.Deserialize(sar);

            BigInteger locked = sarInfo.locked;
            BigInteger hasDrawed = sarInfo.hasDrawed;

            //当前NEO美元价格，需要从价格中心获取
            BigInteger neoPrice = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = CONFIG_PRICE_NEO;
                neoPrice = (BigInteger)OracleContract("getPrice", arg);
            }

            //当前兑换率，需要从配置中心获取
            BigInteger rate = getConfig(CONFIG_RATE);
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = CONFIG_RATE_C;
                rate = (BigInteger)OracleContract("getConfig", arg);
            }

            //计算已经兑换过的PNEO量
            BigInteger hasDrawPNeo = hasDrawed * rate * SIX_POWER / neoPrice;

            //释放的总量大于已经剩余，不能操作
            if (mount > locked - hasDrawPNeo) return false;

            byte[] from = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));
            if (from.Length == 0) return false;
            {
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = addr;
                arg[2] = mount;
                var WContract = (NEP5Contract)wAssetID.ToDelegate();

                if (!(bool)WContract("transfer_contract", arg)) return false;
            }

            //重新设置锁定量
            sarInfo.locked = locked - mount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            //记录交易详细数据
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = sarInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_FREE;
            detail.operated = mount;
            detail.hasLocked = locked - mount;
            detail.hasDrawed = hasDrawed;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_FREE, mount);
            return true;
        }

        private static Boolean openSAR4C(byte[] addr)
        {
            //已经有SAR就不重新建
            byte[] key = getSARKey(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length > 0)
                return false;

            //交易ID 
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            //交易信息
            SARInfo sarInfo = new SARInfo();
            sarInfo.owner = addr;
            sarInfo.locked = 0;
            sarInfo.hasDrawed = 0;
            sarInfo.txid = txid;

            byte[] txinfo = Helper.Serialize(sarInfo);

            Storage.Put(Storage.CurrentContext, key, txinfo);

            //交易详细信息
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_OPEN;
            detail.operated = 0;
            detail.hasLocked = 0;
            detail.hasDrawed = 0;

            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(addr, txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_OPEN,0);
            return true;
        }

        private static Boolean reserve(byte[] wAddr,byte[] addr, BigInteger lockMount)
        {
            if (lockMount <= 0) return false;

            //SAR是否存在
            var key = getSARKey(addr);

            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0)
                return false;
            SARInfo sarInfo = (SARInfo)Helper.Deserialize(sar);

            byte[] to = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));
            if (to.Length == 0) return false;

            object[] arg = new object[3];
            arg[0] = addr;
            arg[1] = to;
            arg[2] = lockMount;

            var WContract = (NEP5Contract)wAddr.ToDelegate();

            if (!(bool)WContract("transfer", arg)) return false;

            //设置锁仓的数量
            BigInteger currLock = sarInfo.locked;
            sarInfo.locked = currLock + lockMount;
            Storage.Put(Storage.CurrentContext, key,Helper.Serialize(sarInfo));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            //记录交易详细数据
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = sarInfo.txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_LOCK;
            detail.operated = lockMount;
            detail.hasLocked = currLock;
            detail.hasDrawed = sarInfo.hasDrawed;
            detail.txid = txid;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_LOCK, lockMount);
            return true;
        }


        private static Boolean expande(byte[] oracleAssetID,byte[] sdusdAssetID, byte[] addr, BigInteger drawMount)
        {
            if (drawMount <= 0) return false;

            //SAR是否存在
            var key = getSARKey(addr);

            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0)
                return false;
            SARInfo sarInfo = (SARInfo)Helper.Deserialize(sar);

            BigInteger locked = sarInfo.locked;
            BigInteger hasDrawed = sarInfo.hasDrawed;

            //当前NEO美元价格，需要从价格中心获取
            BigInteger neoPrice = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = CONFIG_PRICE_NEO;
                neoPrice = (BigInteger)OracleContract("getPrice", arg);
            }

            //当前兑换率，需要从配置中心获取
            BigInteger rate = getConfig(CONFIG_RATE);
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = CONFIG_RATE_C;
                rate = (BigInteger)OracleContract("getConfig", arg);
            }

            //计算总共能兑换的量
            BigInteger allSd = locked * neoPrice/(rate * SIX_POWER);
            
            //超过兑换上限，不能操作
            if (allSd < hasDrawed + drawMount) return false;

            //增加金额
            {
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[2];
                arg[0] = addr;
                arg[1] = drawMount;
                if (!(bool)SDUSDContract("increase", arg)) return false;
            }
            //增加总金额
            //operateTotalSupply(drawMount);
            //记录总生成量
            //recordTotalGenerate(drawMount);

            //设置已经获取量
            sarInfo.hasDrawed = hasDrawed + drawMount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            //记录交易详细数据
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = sarInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_DRAW;
            detail.operated = drawMount;
            detail.hasLocked = locked;
            detail.hasDrawed = hasDrawed;
            
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_DRAW, drawMount);
            return true;

        }

        private static Boolean close(byte[] wAssetID, byte[] sdusdAssetID,byte[] addr)
        {
            if (addr.Length != 20) return false;

            //CDP是否存在
            var key = new byte[] { 0x12 }.Concat(addr);

            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0)
                return false;
            SARInfo sarInfo = (SARInfo)Helper.Deserialize(sar);

            BigInteger locked = sarInfo.locked;
            BigInteger hasDrawed = sarInfo.hasDrawed;

            //当前余额必须要大于负债
            BigInteger balance = 0;
            {
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = addr;
                balance = (BigInteger)SDUSDContract("balanceOf", arg);
            }

            if (hasDrawed > balance) return false;

            byte[] from = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));
            if (from.Length == 0) return false;

            if (hasDrawed > 0)
            {
                //销毁等量SDUSD
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[2];
                arg[0] = addr;
                arg[1] = hasDrawed;
                if (!(bool)SDUSDContract("destory", arg)) return false;
            }

            //返回相应的W资产
            if (locked > 0)
            {
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = addr;
                arg[2] = locked;
                var WContract = (NEP5Contract)wAssetID.ToDelegate();

                if (!(bool)WContract("transfer_contract", arg)) return false;
            }

            //关闭CDP
            Storage.Delete(Storage.CurrentContext, key);

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            //记录交易详细数据
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = sarInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_SHUT;
            detail.operated = hasDrawed;
            detail.hasLocked = locked;
            detail.hasDrawed = hasDrawed;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_SHUT, 0);
            return true;
        }

        private static bool give(byte[] fromAdd, byte[] toAdd)
        {
            //SAR是否存在
            var keyFrom = getSARKey(fromAdd);

            byte[] sar = Storage.Get(Storage.CurrentContext, keyFrom);
            if (sar.Length == 0)
                return false;
            SARInfo fromSAR = (SARInfo)Helper.Deserialize(sar);

            var keyTo = getSARKey(toAdd);
            byte[] sarTo = Storage.Get(Storage.CurrentContext, keyTo);
            if (sarTo.Length > 0)
                return false;

            //删除SAR
            Storage.Delete(Storage.CurrentContext, keyFrom);

            //设置新的SAR
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            fromSAR.owner = toAdd;
            fromSAR.txid = txid;
            Storage.Put(Storage.CurrentContext, keyTo, Helper.Serialize(fromSAR));

            //记录操作信息
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = toAdd;
            detail.sarTxid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_GIVE;
            detail.operated = 0;
            detail.hasLocked = fromSAR.locked;
            detail.hasDrawed = fromSAR.hasDrawed;
            detail.txid = txid;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(fromAdd, fromSAR.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_GIVE, 0);
            return true;
        }


        //private static bool recordTotalGenerate(BigInteger drawMount)
        //{
        //    BigInteger curr = Storage.Get(Storage.CurrentContext, getAccountKey(TOTAL_GENERATE.AsByteArray())).AsBigInteger();
        //    Storage.Put(Storage.CurrentContext, getAccountKey(TOTAL_GENERATE.AsByteArray()), curr + drawMount);
        //    return true;
        //}

        /// <summary>
        ///   This method defines some params to set key.
        /// </summary>
        /// <param name="n">
        ///     0:openSAR 1:lock 
        /// </param>
        /// <returns>
        ///     Return byte[]
        /// </returns>
        private static byte[] ConvertN(BigInteger n)
        {
            if (n == 0)
                return new byte[2] {0x00,0x00};
            if (n == 1)
                return new byte[2] { 0x00, 0x01 };
            if (n == 2)
                return new byte[2] { 0x00, 0x02 };
            if (n == 3)
                return new byte[2] { 0x00, 0x03 };
            if (n == 4)
                return new byte[2] { 0x00, 0x04 };
            throw new Exception("not support.");
        }

    

        //public static TransferInfo getTXInfo(byte[] txid)
        //{
        //    byte[] v = Storage.Get(Storage.CurrentContext, new byte[] { 0x13 }.Concat(txid));
        //    if (v.Length == 0)
        //        return null;
        //    //新式实现方法只要一行
        //    return (TransferInfo)Helper.Deserialize(v);
        //}

        //private static void setTxInfo(byte[] from, byte[] to, BigInteger value)
        //{
        //    TransferInfo info = new TransferInfo();
        //    info.from = from;
        //    info.to = to;
        //    info.value = value;
        //    byte[] txinfo = Helper.Serialize(info);

        //    var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
        //    var keyTxid = new byte[] { 0x13 }.Concat(txid);
        //    Storage.Put(Storage.CurrentContext, keyTxid, txinfo);
        //}

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

        public class SARInfo
        {

            //创建者
            public byte[] owner;

            //交易序号
            public byte[] txid;

            //被锁定的资产,如PNeo
            public BigInteger locked;

            //已经提取的资产，如SDUSDT  
            public BigInteger hasDrawed;

            //1安全  2不安全 3不可用   
            public int status;
        }

        public class SARTransferDetail
        {
            //地址
            public byte[] from;

            //SAR交易序号
            public byte[] sarTxid;

            //交易序号
            public byte[] txid;

            //操作对应资产的金额,如PNeo
            public BigInteger operated;

            //已经被锁定的资产金额,如PNeo
            public BigInteger hasLocked;

            //已经提取的资产金额，如SDUSD
            public BigInteger hasDrawed;

            //操作类型
            public int type;
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

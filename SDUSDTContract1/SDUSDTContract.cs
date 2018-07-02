using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework.Services.System;

namespace SDUSDTContract1
{
    public class SDUSDTContract : SmartContract
    {
        //NEO Asset
        private static readonly byte[] neo_asset_id = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };

        private static readonly byte[] gas_asset_id = Helper.HexToBytes("e72d286979ee6cb1b7e65dfddfb2e384100b8d148e7758de42e4168b71792c60");

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        [DisplayName("approve")]
        public static event Action<byte[], byte[], BigInteger> Approved;


        //超级管理员账户
        private static readonly byte[] SuperAdmin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

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

        //交易类型
        public enum ConfigTranType {
            TRANSACTION_TYPE_LOCK = 1,//锁仓
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
                //允许赋权操作的金额
                if (operation == "allowance")
                {
                    //args[0]发起人账户   args[1]被授权账户
                    return allowance((byte[])args[0], (byte[])args[1]);
                }
                if (operation == "approve")
                {
                    //args[0]发起人账户  args[1]被授权账户   args[2]被授权金额
                    return approve((byte[])args[0], (byte[])args[1], (BigInteger)args[2]);
                }
                if (operation == "transferFrom")
                {
                    //args[0]转账账户  args[1]被授权账户 args[2]被转账账户   args[3]被授权金额
                    return transferFrom((byte[])args[0], (byte[])args[1], (byte[])args[2], (BigInteger)args[3]);
                }
                if (operation == "getTXInfo")
                {
                    if (args.Length != 1) return 0;
                    byte[] txid = (byte[])args[0];
                    return getTXInfo(txid);
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
                //创建SAR记录
                if (operation == "openSAR4C")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];
                    return openSAR(addr);
                }
                //查询债仓记录
                if (operation == "getSAR4C")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];
                    return getSAR(addr);
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
                    return reserve(addr, mount);
                }
                //提取SDUSDT
                if (operation == "expande")
                {
                    if (args.Length != 2) return false;

                    byte[] addr = (byte[])args[0];
                    BigInteger mount = (BigInteger)args[1];
                    return expande(addr, mount);
                }
                //释放未被兑换的PNEO
                if (operation == "withdraw")
                {
                    if (args.Length != 2) return false;

                    byte[] addr = (byte[])args[0];
                    BigInteger mount = (BigInteger)args[1];
                    return withdraw(addr, mount);
                }
                //赎回质押的PNEO，用SDUSD去兑换
                if (operation == "contract")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger mount = (BigInteger)args[1];
                    return contract(addr, mount);
                }
                //关闭在仓
                if (operation == "close")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];
                    return close(addr);
                }
                //强制关闭在仓，由别人发起
                if (operation == "bite")
                {
                    if (args.Length != 2) return false;
                    byte[] otherAddr = (byte[])args[0];
                    byte[] addr = (byte[])args[1];
                    return bite(otherAddr, addr);
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
                    return redeem(addr);
                }
                //转移CDP所有权给其它地址
                if (operation == "give")
                {
                    if (args.Length != 2) return false;
                    byte[] fromAdd = (byte[])args[0];
                    byte[] toAdd = (byte[])args[1];
                    return give(fromAdd, toAdd);
                }
                //计算总生成数量
                if (operation == "totalGenerate")
                {
                    return totalGenerate();
                }
                //测试增加sdt
                if (operation == "mintSDT")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger mount = (BigInteger)args[1];
                    object[] param = new object[2];
                    param[0] = addr;
                    param[1] = mount;
                    if (!Runtime.CheckWitness(addr)) return false;
                    if (!(bool)JumpCenterContract("mint", param)) return false;

                    return true;
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
            return "Special Drawing USD";
        }
        public static string symbol()
        {
            return "SDUSD";
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
            return Storage.Get(Storage.CurrentContext, address).AsBigInteger();
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

            //付款方
            if (from.Length > 0)
            {
                BigInteger from_value = Storage.Get(Storage.CurrentContext, from).AsBigInteger();
                if (from_value < value) return false;
                if (from_value == value)
                    Storage.Delete(Storage.CurrentContext, from);
                else
                    Storage.Put(Storage.CurrentContext, from, from_value - value);
            }
            //收款方
            if (to.Length > 0)
            {
                BigInteger to_value = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
                Storage.Put(Storage.CurrentContext, to, to_value + value);
            }
            //记录交易信息
            setTxInfo(from, to, value);
            //notify
            Transferred(from, to, value);
            return true;
        }

        private static BigInteger totalGenerate()
        {
            return Storage.Get(Storage.CurrentContext, TOTAL_GENERATE).AsBigInteger();
        }

        private static bool give(byte[] fromAdd, byte[] toAdd)
        {
            if (!Runtime.CheckWitness(fromAdd)) return false;
            //SAR是否存在
            var keyFrom = fromAdd.Concat(ConvertN(0));
            byte[] cdp = Storage.Get(Storage.CurrentContext, keyFrom);
            if (cdp.Length == 0)
                return false;
            SARTransferInfo fromCDP = (SARTransferInfo)Helper.Deserialize(cdp);

            var keyTo = toAdd.Concat(ConvertN(0));
            byte[] cdpTo = Storage.Get(Storage.CurrentContext, keyTo);
            if (cdpTo.Length > 0)
                return false;

            //删除SAR
            Storage.Delete(Storage.CurrentContext, keyFrom);

            //设置新的SAR
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            fromCDP.from = toAdd;
            fromCDP.txid = txid;
            Storage.Put(Storage.CurrentContext, keyTo, Helper.Serialize(fromCDP));

            //记录操作信息
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = toAdd;
            detail.cdpTxid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_GIVE;
            detail.operated = 0;
            detail.hasLocked = fromCDP.locked;
            detail.hasDrawed = fromCDP.hasDrawed;
            detail.txid = txid;

            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
            return true;
        }

        public static bool operateTotalSupply(BigInteger mount)
        {
            BigInteger current = Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
            if (current + mount >= 0) {
                Storage.Put(Storage.CurrentContext,TOTAL_SUPPLY, current + mount);
            }
            return true;
        }

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


        private static bool redeem(byte[] addr)
        {
            if (!Runtime.CheckWitness(addr)) return false;

            //被清仓用户剩余PNEO所得
            var otherKey = addr.Concat(ConvertN(1));
            BigInteger currentRemain = Storage.Get(Storage.CurrentContext, otherKey).AsBigInteger();
            if (currentRemain <= 0)
            {
                return false;
            }
            //保存剩余PNEO金额
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            //拿到该有的PNEO
            object[] param = new object[3];
            param[0] = addr;
            param[1] = txid;
            param[2] = currentRemain;
            if (!(bool)JumpCenterContract("increase", param)) return false;
       
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

        private static SARTransferInfo getSAR(byte[] addr)
        {
            //SAR是否存在
            var key = addr.Concat(ConvertN(0));
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0)
                return null;
            SARTransferInfo cdpInfo = (SARTransferInfo)Helper.Deserialize(sar);
            return cdpInfo;
        }

        private static Boolean bite(byte[] otherAddr, byte[] addr)
        {
            if (!Runtime.CheckWitness(addr)) return false;
            //SAR是否存在
            var key = otherAddr.Concat(ConvertN(0));

            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0)
                return false;
            SARTransferInfo cdpInfo = (SARTransferInfo)Helper.Deserialize(sar);

            BigInteger lockedPneo = cdpInfo.locked;
            BigInteger hasDrawed = cdpInfo.hasDrawed;

            //不需要清算
            if (hasDrawed <= 0) return false;

            //余额SDUSD是否足够
            BigInteger myBalance = Storage.Get(Storage.CurrentContext, addr).AsBigInteger();
            if (hasDrawed > myBalance) return false;

            //当前清算折扣比例
            BigInteger rateClear = Storage.Get(Storage.CurrentContext, CONFIG_CLEAR_RATE.AsByteArray()).AsBigInteger();

            //当前兑换率，需要从配置中心获取
            BigInteger rate = Storage.Get(Storage.CurrentContext, CONFIG_RATE.AsByteArray()).AsBigInteger();

            //当前NEO美元价格，需要从价格中心获取
            BigInteger neoPrice = Storage.Get(Storage.CurrentContext, CONFIG_PRICE_NEO.AsByteArray()).AsBigInteger();

            //计算可以拿到的pneo
            BigInteger canClearPneo = hasDrawed * rateClear / (neoPrice * 100);

            //剩余的PNEO记录到原用户账户下
            BigInteger remain = lockedPneo - canClearPneo;
            if (remain < 0) return false;

            //销毁等量SDUSD
            transfer(addr, null, hasDrawed);
            
            //总量处理
            BigInteger current = Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
            if (current - hasDrawed >= 0)
            {
                Storage.Put(Storage.CurrentContext, TOTAL_SUPPLY, current - hasDrawed);
            }

            //拿到该有的PNEO
            //保存剩余PNEO金额
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
  
            object[] param = new object[3];
            param[0] = addr;
            param[1] = txid;
            param[2] = canClearPneo;
            if (!(bool)JumpCenterContract("increase",param)) return false;

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
            detail.cdpTxid = cdpInfo.txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_FORCESHUT;
            detail.operated = hasDrawed;
            detail.hasLocked = lockedPneo;
            detail.hasDrawed = hasDrawed;
            detail.txid = txid;
            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
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
            if (!Runtime.CheckWitness(SuperAdmin)) return false;

            Storage.Put(Storage.CurrentContext,key.AsByteArray(),value);
            return true;
        }

        private static Boolean close(byte[] addr)
        {
            if (addr.Length != 20) return false;
            if (!Runtime.CheckWitness(addr)) return false;
            //CDP是否存在
            var key = addr.Concat(ConvertN(0));

            byte[] cdp = Storage.Get(Storage.CurrentContext, key);
            if (cdp.Length == 0)
                return false;
            SARTransferInfo cdpInfo = (SARTransferInfo)Helper.Deserialize(cdp);

            BigInteger locked = cdpInfo.locked;
            BigInteger hasDrawed = cdpInfo.hasDrawed;
            
            //当前余额必须要大于负债
            BigInteger balance = balanceOf(addr);
            if (hasDrawed > balance) return false;
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            if (locked > 0)
            {
                object[] param = new object[3];
                param[0] = addr;
                param[1] = txid;
                param[2] = locked;
                if (!(bool)JumpCenterContract("increase", param)) return false;
            }

            if (hasDrawed > 0)
            {
                //先要销毁SD
                transfer(addr, null, hasDrawed);
                //减去总量
                operateTotalSupply(0 - hasDrawed);
            }
            //关闭CDP
            Storage.Delete(Storage.CurrentContext,key);

            //记录交易详细数据
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.cdpTxid = cdpInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_SHUT;
            detail.operated = hasDrawed;
            detail.hasLocked = locked;
            detail.hasDrawed = hasDrawed;
            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
            return true;
        }

        private static Boolean contract(byte[] addr, BigInteger mount)
        {
            if (addr.Length != 20) return false;
            if (mount <= 0) return false;
            if (!Runtime.CheckWitness(addr)) return false;

            //CDP是否存在
            var key = addr.Concat(ConvertN(0));

            byte[] cdp = Storage.Get(Storage.CurrentContext, key);
            if (cdp.Length == 0)
                return false;
            SARTransferInfo cdpInfo = (SARTransferInfo)Helper.Deserialize(cdp);

            BigInteger locked = cdpInfo.locked;
            BigInteger hasDrawed = cdpInfo.hasDrawed;

            //SD赎回量要小于已经在仓的
            if (mount > hasDrawed) return false;

            //减少金额
            transfer(addr,null, mount);
            //减少总量
            operateTotalSupply(0- mount);

            cdpInfo.hasDrawed = hasDrawed - mount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(cdpInfo));


            //记录交易详细数据
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.cdpTxid = cdpInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_WIPE;
            detail.operated = mount;
            detail.hasLocked = locked;
            detail.hasDrawed = hasDrawed;
            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
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
        private static Boolean withdraw(byte[] addr, BigInteger mount)
        {
            if (mount <= 0) return false;
            if (!Runtime.CheckWitness(addr)) return false;

            //CDP是否存在
            var key = addr.Concat(ConvertN(0));

            byte[] cdp = Storage.Get(Storage.CurrentContext, key);
            if (cdp.Length == 0)
                return false;
            SARTransferInfo cdpInfo = (SARTransferInfo)Helper.Deserialize(cdp);

            BigInteger locked = cdpInfo.locked;
            BigInteger hasDrawed = cdpInfo.hasDrawed;

            //当前NEO美元价格，需要从价格中心获取
            BigInteger neoPrice = getConfig(CONFIG_PRICE_NEO);
            //当前兑换率，需要从配置中心获取
            BigInteger rate = getConfig(CONFIG_RATE);

            //计算已经兑换过的PNEO量
            BigInteger hasDrawPNeo = hasDrawed * rate / (100 * neoPrice);

            //释放的总量大于已经剩余，不能操作
            if (mount > locked - hasDrawPNeo) return false;

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            object[] param = new object[3];
            param[0] = addr;
            param[1] = txid;
            param[2] = mount;
            if (!(bool)JumpCenterContract("increase", param)) return false;

            //重新设置锁定量
            cdpInfo.locked = locked - mount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(cdpInfo));

            //记录交易详细数据
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.cdpTxid = cdpInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_FREE;
            detail.operated = mount;
            detail.hasLocked = locked - mount;
            detail.hasDrawed = hasDrawed;
            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
            return true;
        }

        private static Boolean openSAR(byte[] addr)
        {
            if (!Runtime.CheckWitness(addr)) return false;

            //已经有SAR就不重新建
            SARTransferInfo cdpInfo = getSAR(addr);
            if (cdpInfo != null) return false;

            //交易ID 
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            //交易信息
            cdpInfo = new SARTransferInfo();
            cdpInfo.from = addr;
            cdpInfo.locked = 0;
            cdpInfo.hasDrawed = 0;
            cdpInfo.txid = txid;

            byte[] key = addr.Concat(ConvertN(0));
            byte[] txinfo = Helper.Serialize(cdpInfo);
            Storage.Put(Storage.CurrentContext, key, txinfo);
            return true;
        }

        private static Boolean reserve(byte[] addr, BigInteger lockMount)
        {
            if (lockMount <= 0) return false;
            if (!Runtime.CheckWitness(addr)) return false;

            //CDP是否存在
            var key = addr.Concat(ConvertN(0));

            byte[] cdp = Storage.Get(Storage.CurrentContext, key);
            if (cdp.Length == 0)
                return false;
            SARTransferInfo cdpInfo = (SARTransferInfo)Helper.Deserialize(cdp);

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            //销毁PNeo
            //Storage.Put(Storage.CurrentContext,txid,lockMount);
            object[] param = new object[3];
            param[0] = addr;
            param[1] = txid;
            param[2] = lockMount;
            if (!(bool)JumpCenterContract("destory", param)) return false;

            //设置锁仓的数量
            BigInteger currLock = cdpInfo.locked;
            cdpInfo.locked = currLock + lockMount;
            Storage.Put(Storage.CurrentContext, key,Helper.Serialize(cdpInfo));

            //记录交易详细数据
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.cdpTxid = cdpInfo.txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_LOCK;
            detail.operated = lockMount;
            detail.hasLocked = currLock;
            detail.hasDrawed = cdpInfo.hasDrawed;
            detail.txid = txid;
            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));

            return true;
        }


        private static Boolean expande(byte[] addr, BigInteger drawMount)
        {
            if (drawMount <= 0) return false;
            if (!Runtime.CheckWitness(addr)) return false;

            //CDP是否存在
            var key = addr.Concat(ConvertN(0));

            byte[] cdp = Storage.Get(Storage.CurrentContext, key);
            if (cdp.Length == 0)
                return false;
            SARTransferInfo cdpInfo = (SARTransferInfo)Helper.Deserialize(cdp);

            BigInteger locked = cdpInfo.locked;
            BigInteger hasDrawed = cdpInfo.hasDrawed;

            //当前NEO美元价格，需要从价格中心获取
            BigInteger neoPrice = getConfig(CONFIG_PRICE_NEO);
            //当前兑换率，需要从配置中心获取
            BigInteger rate = getConfig(CONFIG_RATE);
            //计算总共能兑换的量
            BigInteger allSd =locked * neoPrice*100/rate;
            
            //超过兑换上限，不能操作
            if (allSd < hasDrawed + drawMount) return false;

            //增加金额
            transfer(null, addr, drawMount);
            //增加总金额
            operateTotalSupply(drawMount);
            //记录总生成量
            recordTotalGenerate(drawMount);

            //设置已经获取量
            cdpInfo.hasDrawed = hasDrawed + drawMount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(cdpInfo));

            //记录交易详细数据
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.cdpTxid = cdpInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_DRAW;
            detail.operated = drawMount;
            detail.hasLocked = locked;
            detail.hasDrawed = hasDrawed;
            
            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
            return true;

        }

        private static bool recordTotalGenerate(BigInteger drawMount)
        {
            BigInteger curr = Storage.Get(Storage.CurrentContext, TOTAL_GENERATE).AsBigInteger();
            Storage.Put(Storage.CurrentContext, TOTAL_GENERATE, curr + drawMount);
            return true;
        }

        /// <summary>
        ///   This method defines some params to set key.
        /// </summary>
        /// <param name="n">
        ///     0:openCDP 1:lock 
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

    

        public static TransferInfo getTXInfo(byte[] txid)
        {
            byte[] v = Storage.Get(Storage.CurrentContext, txid);
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
            Storage.Put(Storage.CurrentContext, txid, txinfo);
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

        public class SARTransferInfo
        {
            //地址
            public byte[] from;

            //交易序号
            public byte[] txid;

            //被锁定的资产,如PNeo
            public BigInteger locked;

            //已经提取的资产，如SDUSDT  
            public BigInteger hasDrawed;
        }

        public class SARTransferDetail
        {
            //地址
            public byte[] from;

            //CDP交易序号
            public byte[] cdpTxid;

            //交易序号
            public byte[] txid;

            //操作对应资产的金额,如PNeo
            public BigInteger operated;

            //已经被锁定的资产金额,如PNeo
            public BigInteger hasLocked;

            //已经提取的资产金额，如SDUSDT  
            public BigInteger hasDrawed;

            //操作类型
            public int type;
        }

        /// <summary>
        ///   Return the amount of the tokens that the spender could transfer from the owner acount
        /// </summary>
        /// <param name="owner">
        ///   The account to invoke the Approve method
        /// </param>
        /// <param name="spender">
        ///   The account to grant TransferFrom access to.
        /// </param>
        /// <returns>
        ///   The amount to grant TransferFrom access for
        /// </returns>
        public static BigInteger allowance(byte[] owner, byte[] spender)
        {
            return Storage.Get(Storage.CurrentContext, owner.Concat(spender)).AsBigInteger();
        }

        /// <summary>
        ///   Approve another account to transfer amount tokens from the owner acount by transferForm
        /// </summary>
        /// <param name="owner">
        ///   The account to invoke approve.
        /// </param>
        /// <param name="spender">
        ///   The account to grant TransferFrom access to.
        /// </param>
        /// <param name="amount">
        ///   The amount to grant TransferFrom access for.
        /// </param>
        /// <returns>
        ///   Transaction Successful?
        /// </returns>
        public static bool approve(byte[] owner, byte[] spender, BigInteger amount)
        {
            if (owner.Length != 20 || spender.Length != 20) return false;
            if (!Runtime.CheckWitness(owner)) return false;
            if (owner == spender) return true;
            if (amount < 0) return false;
            if (amount == 0)
            {
                Storage.Delete(Storage.CurrentContext, owner.Concat(spender));
                Approved(owner, spender, amount);
                return true;
            }
            Storage.Put(Storage.CurrentContext, owner.Concat(spender), amount);
            Approved(owner, spender, amount);
            return true;
        }

        /// <summary>
        ///   Transfer an amount from the owner account to the to acount if the spender has been approved to transfer the requested amount
        /// </summary>
        /// <param name="owner">
        ///   The account to transfer a balance from.
        /// </param>
        /// <param name="spender">
        ///   The contract invoker.
        /// </param>
        /// <param name="to">
        ///   The account to transfer a balance to.
        /// </param>
        /// <param name="amount">
        ///   The amount to transfer
        /// </param>
        /// <returns>
        ///   Transaction successful?
        /// </returns>
        public static bool transferFrom(byte[] owner, byte[] spender, byte[] to, BigInteger amount)
        {
            if (owner.Length != 20 || spender.Length != 20 || to.Length != 20) return false;
            if (!Runtime.CheckWitness(spender)) return false;
            BigInteger allowance = Storage.Get(Storage.CurrentContext, owner.Concat(spender)).AsBigInteger();
            BigInteger fromOrigBalance = Storage.Get(Storage.CurrentContext, owner).AsBigInteger();
            BigInteger toOrigBalance = Storage.Get(Storage.CurrentContext, to).AsBigInteger();

            if (amount >= 0 &&
                allowance >= amount &&
                fromOrigBalance >= amount)
            {
                if (allowance - amount == 0)
                {
                    Storage.Delete(Storage.CurrentContext, owner.Concat(spender));
                }
                else
                {
                    Storage.Put(Storage.CurrentContext, owner.Concat(spender), IntToBytes(allowance - amount));
                }

                if (fromOrigBalance - amount == 0)
                {
                    Storage.Delete(Storage.CurrentContext, owner);
                }
                else
                {
                    Storage.Put(Storage.CurrentContext, owner, IntToBytes(fromOrigBalance - amount));
                }

                Storage.Put(Storage.CurrentContext, to, IntToBytes(toOrigBalance + amount));
                Transferred(owner, to, amount);
                return true;
            }
            return false;
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

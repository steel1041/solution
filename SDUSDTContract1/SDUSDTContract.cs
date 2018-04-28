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

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        [DisplayName("approve")]
        public static event Action<byte[], byte[], BigInteger> Approved;


        //超级管理员账户
        private static readonly byte[] SuperAdmin = Helper.ToScriptHash("AHBL6ojH9Tb5U7VCWuGrNjHBGQPfjd33Xe");

        //调用PNeo合约
        [Appcall("eb5e687828caff219738a60f632d4ba08027bf29")]
        public static extern Boolean PNeoContract(string operation, params object[] args);

        //nep5 func
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
        }
        public static string Name()
        {
            return "SD USDT";
        }
        public static string Symbol()
        {
            return "SDUSDT";
        }
        //因子
        private const ulong factor = 100000000;

        //总计数量
        private const ulong TOTAL_AMOUNT = 0;

        private const string TOTAL_SUPPLY = "totalSupply";

        //交易类型-锁仓
        private const string TRANSACTION_TYPE_LOCK = "1";

        //交易类型-提取
        private const string TRANSACTION_TYPE_DRAW = "2";

        //交易类型-释放
        private const string TRANSACTION_TYPE_FREE = "3";

        //交易类型-赎回
        private const string TRANSACTION_TYPE_WIPE = "4";

        //交易类型-关闭
        private const string TRANSACTION_TYPE_SHUT = "5";

        //交易类型-对手关闭
        private const string TRANSACTION_TYPE_FORCESHUT = "6";

        //配置参数-兑换比率，百分位，如150、200
        private const string CONFIG_RATE = "rate";

        //配置参数-NEO市场价格
        private const string CONFIG_PRICE_NEO = "neo_price";

        //配置参数-GAS市场价格
        private const string CONFIG_PRICE_GAS = "gas_price";


        public static byte Decimals()
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
        public static BigInteger BalanceOf(byte[] address)
        {
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
        public static bool Transfer(byte[] from, byte[] to, BigInteger value)
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
            var magicstr = "2018-04-25 14:40:10";

            if (Runtime.Trigger == TriggerType.Verification)//取钱才会涉及这里
            {
                return Runtime.CheckWitness(SuperAdmin);

            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                //必须在入口函数取得callscript，调用脚本的函数，也会导致执行栈变化，再取callscript就晚了
                var callscript = ExecutionEngine.CallingScriptHash;
                //this is in nep5
                if (operation == "totalSupply") return TotalSupply();
                if (operation == "name") return Name();
                if (operation == "symbol") return Symbol();
                if (operation == "decimals") return Decimals();
                if (operation == "balanceOf")
                {
                    if (args.Length != 1) return 0;
                    byte[] account = (byte[])args[0];
                    return BalanceOf(account);
                }

                if (operation == "transfer")
                {
                    if (args.Length != 3) return false;
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    if (from == to)
                        return true;
                    if (from.Length == 0 || to.Length == 0)
                        return false;
                    BigInteger value = (BigInteger)args[2];
                    //没有from签名，不让转
                    if (!Runtime.CheckWitness(from))
                        return false;
                    //如果有跳板调用，不让转
                    if (ExecutionEngine.EntryScriptHash.AsBigInteger() != callscript.AsBigInteger())
                        return false;
                    return Transfer(from, to, value);
                }
                //允许赋权操作的金额
                if (operation == "allowance")
                {
                    //args[0]发起人账户   args[1]被授权账户
                    return Allowance((byte[])args[0], (byte[])args[1]);
                }
                if (operation == "approve")
                {
                    //args[0]发起人账户  args[1]被授权账户   args[2]被授权金额
                    return Approve((byte[])args[0], (byte[])args[1], (BigInteger)args[2]);
                }
                if (operation == "transferFrom")
                {
                    //args[0]转账账户  args[1]被授权账户 args[2]被转账账户   args[3]被授权金额
                    return TransferFrom((byte[])args[0], (byte[])args[1], (byte[])args[2], (BigInteger)args[3]);
                }
                if (operation == "getTXInfo")
                {
                    if (args.Length != 1) return 0;
                    byte[] txid = (byte[])args[0];
                    return GetTXInfo(txid);
                }
                //设置全局参数
                if (operation == "setConfig") {
                    if (args.Length != 2) return false;
                    string key = (string)args[0];
                    BigInteger value = (BigInteger)args[1];
                    return SetConfig(key,value);
                }
                //查询全局参数
                if (operation == "getConfig")
                {
                    if (args.Length != 1) return false;
                    string key = (string)args[0];
   
                    return GetConfig(key);
                }
                //创建CDP记录
                if (operation == "openCdp")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];
                    return OpenCDP(addr);
                }
                //锁仓PNeo
                if (operation=="lock") {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger lockMount = (BigInteger)args[1];
                    return LockMount(addr,lockMount);
                }
                //提取SDUSDT
                if (operation == "draw") {
                    if (args.Length != 2) return false;

                    byte[] addr = (byte[])args[0];
                    BigInteger drawMount = (BigInteger)args[1];
                    return Draw(addr, drawMount);
                }
                //释放未被兑换的PNEO
                if (operation == "free") {
                    if (args.Length != 2) return false;

                    byte[] addr = (byte[])args[0];
                    BigInteger freeMount = (BigInteger)args[1];
                    return Free(addr, freeMount);
                }
                //赎回质押的PNEO，用SDUSD去兑换
                if (operation == "wipe"){
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger wipeMount = (BigInteger)args[1];
                    return Wipe(addr, wipeMount);
                }
                //关闭在仓
                if (operation == "shut") {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];
                    return Shut(addr);
                }
                //强制关闭在仓，由别人发起
                if (operation == "forceShut")
                {
                    if (args.Length != 1) return false;
                    byte[] otherAddr = (byte[])args[0];
                    byte[] addr = (byte[])args[1];

                    return ForceShut(otherAddr,addr);
                }
            }
            return false;
        }

        private static Boolean ForceShut(byte[] otherAddr, byte[] addr)
        {
            if (!Runtime.CheckWitness(addr)) return false;
            //CDP是否存在
            var key = otherAddr.Concat(ConvertN(0));
            byte[] cdp = Storage.Get(Storage.CurrentContext, key);
            if (cdp.Length == 0)
                return false;

            CDPTransferInfo cdpInfo = (CDPTransferInfo)Helper.Deserialize(cdp);
            BigInteger locked = cdpInfo.locked;
            BigInteger hasDrawed = cdpInfo.hasDrawed;

            //返还所有PNEO
            if (!PNeoContract("increase", addr, locked)) return false;

            //to do ... 还需要转让剩余的pneo给对方

            //销毁SD
            Transfer(addr, null, hasDrawed*9/10);

            Storage.Delete(Storage.CurrentContext, key);

            //记录交易详细数据
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            CDPTransferDetail detail = new CDPTransferDetail();
            detail.from = addr;
            detail.cdpTxid = cdpInfo.txid;
            detail.type = TRANSACTION_TYPE_FORCESHUT;
            detail.locked = 0;
            detail.hasLocked = locked;
            detail.drawed = hasDrawed;
            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
            return true;
        }

        private static BigInteger GetConfig(string key)
        {
            if (key == null || key == "") return 0;
            return  Storage.Get(Storage.CurrentContext,key.AsByteArray()).AsBigInteger();
        }

        private static Boolean SetConfig(string key, BigInteger value)
        {
            if (key == null || key == "") return false;
            if (!Runtime.CheckWitness(SuperAdmin)) return false;

            Storage.Put(Storage.CurrentContext,key.AsByteArray(),value);
            return true;
        }

        private static Boolean Shut(byte[] addr)
        {
            if (!Runtime.CheckWitness(addr)) return false;
            //CDP是否存在
            var key = addr.Concat(ConvertN(0));
            byte[] cdp = Storage.Get(Storage.CurrentContext, key);
            if (cdp.Length == 0)
                return false;

            CDPTransferInfo cdpInfo = (CDPTransferInfo)Helper.Deserialize(cdp);
            BigInteger locked = cdpInfo.locked;
            BigInteger hasDrawed = cdpInfo.hasDrawed;

            //增发PNEO
            if (!PNeoContract("increase", addr, locked)) return false;

            //先要销毁SD
            Transfer(addr, null, hasDrawed);

            Storage.Delete(Storage.CurrentContext,key);

            //记录交易详细数据
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            CDPTransferDetail detail = new CDPTransferDetail();
            detail.from = addr;
            detail.cdpTxid = cdpInfo.txid;
            detail.type = TRANSACTION_TYPE_SHUT;
            detail.locked = 0;
            detail.hasLocked = locked;
            detail.drawed = hasDrawed;
            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
            return true;

        }

        private static Boolean Wipe(byte[] addr, BigInteger wipeMount)
        {
            if (wipeMount <= 0) return false;
            if (!Runtime.CheckWitness(addr)) return false;

            //CDP是否存在
            var key = addr.Concat(ConvertN(0));
            byte[] cdp = Storage.Get(Storage.CurrentContext, key);
            if (cdp.Length == 0)
                return false;

            CDPTransferInfo cdpInfo = (CDPTransferInfo)Helper.Deserialize(cdp);
            BigInteger locked = cdpInfo.locked;
            BigInteger hasDrawed = cdpInfo.hasDrawed;

            //SD赎回量要小于已经在仓的
            if (wipeMount > hasDrawed) return false;

            //先要销毁SD
            Transfer(addr,null, wipeMount);

            cdpInfo.hasDrawed = hasDrawed - wipeMount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(cdpInfo));


            //记录交易详细数据
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            CDPTransferDetail detail = new CDPTransferDetail();
            detail.from = addr;
            detail.cdpTxid = cdpInfo.txid;
            detail.type = TRANSACTION_TYPE_WIPE;
            detail.locked = 0;
            detail.hasLocked = locked;
            detail.drawed = wipeMount;
            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
            return true;
        }

        /// <summary>
        ///   This method is free pneo asset.
        /// </summary>
        /// <param name="addr">
        ///     The address being invoked.
        /// </param>
        /// <param name="freeMount">
        ///     The mount need free,it's pneo.
        /// </param>
        /// <returns>
        ///     Return Boolean
        /// </returns>
        private static Boolean Free(byte[] addr, BigInteger freeMount)
        {
            if (freeMount <= 0) return false;
            if (!Runtime.CheckWitness(addr)) return false;

            //CDP是否存在
            var key = addr.Concat(ConvertN(0));
            byte[] cdp = Storage.Get(Storage.CurrentContext, key);
            if (cdp.Length == 0)
                return false;

            CDPTransferInfo cdpInfo = (CDPTransferInfo)Helper.Deserialize(cdp);
            BigInteger locked = cdpInfo.locked;
            BigInteger hasDrawed = cdpInfo.hasDrawed;

            //当前NEO美元价格，需要从价格中心获取
            BigInteger neoPrice = GetConfig(CONFIG_PRICE_NEO);
            //当前兑换率，需要从配置中心获取
            BigInteger rate = GetConfig(CONFIG_RATE);

            //计算已经兑换过的PNEO量
            BigInteger hasDrawPNeo = hasDrawed * rate / (100 * neoPrice);

            //释放的总量大于已经剩余，不能操作
            if (freeMount > locked - hasDrawPNeo) return false;

            //增发PNEO
            if (!PNeoContract("increase", addr, freeMount)) return false;

            //重新设置锁定量
            cdpInfo.locked = locked - freeMount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(cdpInfo));

            //记录交易详细数据
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            CDPTransferDetail detail = new CDPTransferDetail();
            detail.from = addr;
            detail.cdpTxid = cdpInfo.txid;
            detail.type = TRANSACTION_TYPE_FREE;
            detail.locked = freeMount;
            detail.hasLocked = locked - freeMount;
            detail.drawed = 0;
            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
            return true;
        }

        private static Boolean OpenCDP(byte[] addr)
        {
            if (!Runtime.CheckWitness(addr)) return false;

            //已经有在仓的CDP就不重新建
            var key = addr.Concat(ConvertN(0));
            byte[] cdp = Storage.Get(Storage.CurrentContext, key);
            if (cdp.Length > 0)
                return false;

            //交易ID 
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            //交易信息
            CDPTransferInfo cdpInfo = new CDPTransferInfo();
            cdpInfo.from = addr;
            cdpInfo.locked = 0;
            cdpInfo.hasDrawed = 0;
            cdpInfo.txid = txid;

            byte[] txinfo = Helper.Serialize(cdpInfo);
            Storage.Put(Storage.CurrentContext, key, txinfo);
            return true;
        }

        private static Boolean LockMount(byte[] addr, BigInteger lockMount)
        {
            if (lockMount <= 0) return false;
            if (!Runtime.CheckWitness(addr)) return false;

            //CDP是否存在
            var key = addr.Concat(ConvertN(0));
            byte[] cdp = Storage.Get(Storage.CurrentContext, key);
            if (cdp.Length == 0) {
                return false;
            }

            //销毁PNeo
            if(!PNeoContract("destory",addr,lockMount))return false;

            //设置锁仓的数量
            CDPTransferInfo cdpInfo = (CDPTransferInfo)Helper.Deserialize(cdp);

            BigInteger currLock = cdpInfo.locked;
            cdpInfo.locked = currLock + lockMount;
            Storage.Put(Storage.CurrentContext, key,Helper.Serialize(cdpInfo));

            //记录交易详细数据
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            CDPTransferDetail detail = new CDPTransferDetail();
            detail.from = addr;
            detail.cdpTxid = cdpInfo.txid;
            detail.type = TRANSACTION_TYPE_LOCK;
            detail.locked = lockMount;
            detail.hasLocked = currLock;
            detail.drawed = 0;
            detail.txid = txid;

            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
            return true;
        }


        private static Boolean Draw(byte[] addr, BigInteger drawMount)
        {
            if (drawMount <= 0) return false;
            if (!Runtime.CheckWitness(addr)) return false;

            //CDP是否存在
            var key = addr.Concat(ConvertN(0));
            byte[] cdp = Storage.Get(Storage.CurrentContext, key);
            if (cdp.Length == 0)
                return false;

            CDPTransferInfo cdpInfo = (CDPTransferInfo)Helper.Deserialize(cdp);
            BigInteger locked = cdpInfo.locked;
            BigInteger hasDrawed = cdpInfo.hasDrawed;

            //当前NEO美元价格，需要从价格中心获取
            BigInteger neoPrice = GetConfig(CONFIG_PRICE_NEO);
            //当前兑换率，需要从配置中心获取
            BigInteger rate = GetConfig(CONFIG_RATE);

            //计算总共能兑换的量
            BigInteger allSd =locked * neoPrice*100/rate;
            
            //超过兑换上限，不能操作
            if (allSd < hasDrawed + drawMount) return false;

            Transfer(null, addr, drawMount);

            //设置已经获取量
            cdpInfo.hasDrawed = hasDrawed + drawMount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(cdpInfo));

            //记录交易详细数据
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            CDPTransferDetail detail = new CDPTransferDetail();
            detail.from = addr;
            detail.cdpTxid = cdpInfo.txid;
            detail.type = TRANSACTION_TYPE_DRAW;
            detail.locked = 0;
            detail.hasLocked = locked;
            detail.drawed = drawMount;
            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
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

    

        public static TransferInfo GetTXInfo(byte[] txid)
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

        public static bool MintTokens()
        {
            var tx = (Transaction)ExecutionEngine.ScriptContainer;
            
            //获取投资人，谁要换gas
            byte[] who = null;
            TransactionOutput[] reference = tx.GetReferences();
            for (var i = 0; i < reference.Length; i++)
            {
                if (reference[i].AssetId.AsBigInteger() == neo_asset_id.AsBigInteger())
                {
                    who = reference[i].ScriptHash;
                    break;
                }
            }

            TransactionOutput[] outputs = tx.GetOutputs();
            ulong value = 0;
            // get the total amount of Neo
            // 获取转入智能合约地址的Gas总量
            foreach (TransactionOutput output in outputs)
            {
                if (output.ScriptHash == ExecutionEngine.ExecutingScriptHash &&
                    output.AssetId.AsBigInteger() == neo_asset_id.AsBigInteger())
                {
                    value += (ulong)output.Value;
                }
            }

            //改变总量
            var total_supply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
            total_supply += value;
            Storage.Put(Storage.CurrentContext, "totalSupply", total_supply);

            //1:1 不用换算
            return Transfer(null, who, value);
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

        public class CDPTransferInfo
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

        public class CDPTransferDetail
        {
            //地址
            public byte[] from;

            //CDP交易序号
            public byte[] cdpTxid;

            //交易序号
            public byte[] txid;

            //需要被锁定的资产,如PNeo
            public BigInteger locked;

            //已经被锁定的资产,如PNeo
            public BigInteger hasLocked;

            //需要提取的资产，如SDUSDT  
            public BigInteger drawed;

            //操作类型
            public string type;
        }

        /// <summary>
        ///   Init the sdt tokens to the SuperAdmin account，only once
        /// </summary>
        /// <returns>
        ///   Transaction Successful?
        /// </returns>
        public static bool Init()
        {
            if (!Runtime.CheckWitness(SuperAdmin)) return false;
            byte[] total_supply = Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY);
            if (total_supply.Length != 0) return false;
            Storage.Put(Storage.CurrentContext, SuperAdmin, IntToBytes(TOTAL_AMOUNT));
            Storage.Put(Storage.CurrentContext, TOTAL_SUPPLY, TOTAL_AMOUNT);
            Transferred(null, SuperAdmin, TOTAL_AMOUNT);
            return true;
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
        public static BigInteger Allowance(byte[] owner, byte[] spender)
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
        public static bool Approve(byte[] owner, byte[] spender, BigInteger amount)
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
        public static bool TransferFrom(byte[] owner, byte[] spender, byte[] to, BigInteger amount)
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

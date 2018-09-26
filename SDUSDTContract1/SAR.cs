using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework.Services.System;

namespace SARContract
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

        /* addr,sartxid,txid,type,operated*/
        [DisplayName("sarOperator4C")]
        public static event Action<byte[], byte[], byte[], BigInteger, BigInteger> Operated;

        public delegate object NEP5Contract(string method, object[] args);

        //管理员账户
        private static readonly byte[] admin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        //因子
        private const ulong factor = 100000000;

        //总计数量
        private const ulong TOTAL_AMOUNT = 0;

        private const string TOTAL_SUPPLY = "totalSupply";

        private const string TOTAL_GENERATE = "totalGenerate";

        //配置参数-SDS市场价格
        private const string CONFIG_PRICE_SDS = "sds_price";

        //配置参数-清算比率，百分位，如90
        private const string CONFIG_CLEAR_RATE = "liquidate_dis_rate_c";

        //最低抵押率 百分位，如150、200
        private const string CONFIG_RATE_C = "liquidate_line_rate_c";

        private const string CONFIG_FEE_C = "fee_rate_c";

        //伺机者清算抵押率
        private const string CONFIG_BOND_C = "liquidate_line_rateT_c";

        //最大发行量
        private const string CONFIG_RELEASE_MAX = "debt_top_c";

        //合约收款账户
        private const string STORAGE_ACCOUNT = "storage_account";   
        
        //新合约收款账户
        private const string STORAGE_ACCOUNT_NEW = "storage_account_new";

        //SDS合约账户
        private const string SDS_ACCOUNT = "sds_account";

        //Oracle合约账户
        private const string ORACLE_ACCOUNT = "oracle_account";

        //SNEO合约账户
        private const string SASSET_ACCOUNT = "sasset_account";

        //SDUSD合约账户
        private const string SDUSD_ACCOUNT = "sdusd_account";

        private const string SAR_STATE = "sar_state";

        private const ulong SIX_POWER = 1000000;

        private const ulong TEN_POWER = 10000000000;

        private static byte[] getSARKey(byte[] addr) => new byte[] { 0x12 }.Concat(addr);

        private static byte[] getTxidKey(byte[] txid) => new byte[] { 0x14 }.Concat(txid);

        private static byte[] getAccountKey(byte[] account) => new byte[] { 0x15 }.Concat(account);

        private static byte[] getBondKey(byte[] account) => new byte[] { 0x16 }.Concat(account);

        private static byte[] getConfigKey(byte[] key) => new byte[] { 0x17 }.Concat(key);

        private static byte[] getRescueKey(byte[] assetType,byte[] addr) => new byte[] { 0x18}.Concat(assetType).Concat(addr);


        //交易类型
        public enum ConfigTranType {
            TRANSACTION_TYPE_OPEN=1,//创建SAR
            TRANSACTION_TYPE_LOCK,//锁仓
            TRANSACTION_TYPE_DRAW,//提取
            TRANSACTION_TYPE_FREE,//释放
            TRANSACTION_TYPE_WIPE,//赎回
            TRANSACTION_TYPE_SHUT,//关闭
            TRANSACTION_TYPE_RESUCE,//清算其它债仓
            TRANSACTION_TYPE_GIVE,//转移所有权
            TRANSACTION_TYPE_BOND_RESUCE,//bond
            TRANSACTION_TYPE_BOND_WITHDRAW,
            TRANSACTION_TYPE_RECHARGE,  //充值手续费
            TRANSACTION_TYPE_CLAIMRESCUE, //签收清算资产
            TRANSACTION_TYPE_CLAIMFEE   //签收手续费
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
            var magicstr = "2018-09-26 14:40:10";

            if (Runtime.Trigger == TriggerType.Verification)//取钱才会涉及这里
            {
                return false;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                //必须在入口函数取得callscript，调用脚本的函数，也会导致执行栈变化，再取callscript就晚了
                var callscript = ExecutionEngine.CallingScriptHash;
               
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
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    string assetType = (string)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return openSAR4C(addr,assetType);
                }
                //创建SAR记录
                if (operation == "batchSAR4C")
                {
                    if (args.Length != 11) return false;
                    byte[] addr = (byte[])args[0];
                    byte[] txid = (byte[])args[1];
                    BigInteger locked = (BigInteger)args[2];
                    BigInteger hasDrawed = (BigInteger)args[3];
                    string assetType = (string)args[4];
                    int status = (int)args[5];
                    BigInteger bondLocked = (BigInteger)args[6];
                    BigInteger bondDrawed = (BigInteger)args[7];
                    uint lastHeight = (uint)args[8];
                    BigInteger fee = (BigInteger)args[9];
                    BigInteger sdsFee = (BigInteger)args[10];

                    if (!Runtime.CheckWitness(admin)) return false;

                    SARInfo sar = new SARInfo();
                    sar.assetType = assetType;
                    sar.bondDrawed = bondDrawed;
                    sar.bondLocked = bondLocked;
                    sar.hasDrawed = hasDrawed;
                    sar.locked = locked;
                    sar.owner = addr;
                    sar.status = status;
                    sar.txid = txid;
                    sar.lastHeight = lastHeight;
                    sar.fee = fee;
                    sar.sdsFee = sdsFee;
                    
                    return batchSAR4C(addr,sar);
                }
                //创建SAR记录
                if (operation == "createSAR4C")
                {
                    if (args.Length != 12) return false;
                    byte[] addr = (byte[])args[0];
                    byte[] txid = (byte[])args[1];
                    BigInteger locked = (BigInteger)args[2];
                    BigInteger hasDrawed = (BigInteger)args[3];
                    string assetType = (string)args[4];
                    int status = (int)args[5];
                    BigInteger bondLocked = (BigInteger)args[6];
                    BigInteger bondDrawed = (BigInteger)args[7];
                    uint lastHeight = (uint)args[8];
                    BigInteger fee = (BigInteger)args[9];
                    BigInteger sdsFee = (BigInteger)args[10];
                    BigInteger rescue = (BigInteger)args[11];

                    if (!Runtime.CheckWitness(addr)) return false;

                    SARInfo sar = new SARInfo();
                    sar.assetType = assetType;
                    sar.bondDrawed = bondDrawed;
                    sar.bondLocked = bondLocked;
                    sar.hasDrawed = hasDrawed;
                    sar.locked = locked;
                    sar.owner = addr;
                    sar.status = status;
                    sar.txid = txid;
                    sar.lastHeight = lastHeight;
                    sar.fee = fee;
                    sar.sdsFee = sdsFee;

                    if (rescue > 0) {
                        Storage.Put(Storage.CurrentContext,getRescueKey(assetType.AsByteArray(),addr),rescue);
                    }
                    return createSAR4C(addr, sar);
                }
                //转移SAR合约中NEP5资产
                if (operation == "migrateSAR4C")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return migrateSAR4C(addr);
                }

                //查询债仓记录
                if (operation == "getSAR4C")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];

                    return Helper.Deserialize(getSAR4C(addr)) as SARInfo;
                }
                //查询债仓详细操作记录
                if (operation == "getSARTxInfo")
                {
                    if (args.Length != 1) return false;
                    byte[] txid = (byte[])args[0];
                    return getSARTxInfo(txid);
                }
                //locked nep5 asset
                if (operation == "reserve")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    //NEP5 Asset mount 
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return reserve(addr, mount);
                }
                //locked sds asset
                if (operation == "recharge")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    //SDS Asset mount 
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return recharge(addr, mount);
                }
                //get SDUSD
                if (operation == "expande")
                {
                    if (args.Length != 2) return false;

                    byte[] addr = (byte[])args[0];
                    //SDUSD
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return expande(addr, mount);
                }
                //get withdraw
                if (operation == "withdraw")
                {
                    if (args.Length != 2) return false;

                    byte[] addr = (byte[])args[0];
                    //NEP5 asset
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return withdraw(addr, mount);
                }
                //释放未被兑换的SNEO
                if (operation == "withdrawT")
                {
                    if (args.Length != 2) return false;

                    byte[] addr = (byte[])args[0];
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return withdrawT(addr, mount);
                }
                //赎回质押的SNEO，用SDUSD去兑换
                if (operation == "contract")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return contract(addr, mount);
                }
                //关闭债仓
                if (operation == "close")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];
 
                    if (!Runtime.CheckWitness(addr)) return false;
                    return close(addr);
                }
                //清算别人债仓，由别人发起
                if (operation == "rescue")
                {
                    if (args.Length != 3) return false;
                    byte[] otherAddr = (byte[])args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger mount = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return rescue(otherAddr, addr,mount);
                }
                //清算账户余额
                if (operation == "getRescue")
                {
                    if (args.Length != 2) return false;
                    string assetType = (string)args[0];
                    byte[] addr = (byte[])args[1];

                    return getRescue(assetType,addr);
                }
                //签收rescue余额
                if (operation == "claimRescue")
                {
                    if (args.Length != 2) return false;
                    string assetType = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    if (!Runtime.CheckWitness(addr)) return false;

                    return claimRescue(assetType, addr);
                }
                //提现剩余手续费
                if (operation == "claimFee")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];
                    if (!Runtime.CheckWitness(addr)) return false;

                    return claimFee(addr);
                }
                if (operation == "rescueT")
                {
                    if (args.Length != 3) return false;
                    byte[] otherAddr = (byte[])args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger mount = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return rescueT(otherAddr, addr, mount);
                }
                if (operation == "setAccount")
                {
                    if (args.Length != 2) return false;
                    string key = (string)args[0];
                    byte[] address = (byte[])args[1];
                    if (!Runtime.CheckWitness(admin)) return false;

                    return setAccount(key, address);
                }

                if (operation == "setBondAccount")
                {
                    if (args.Length != 1) return false;
                    byte[] address = (byte[])args[0];

                    if (!Runtime.CheckWitness(admin)) return false;
                    return setBondAccount(address);
                }

                if (operation == "removeBondAccount")
                {
                    if (args.Length != 1) return false;
                    byte[] address = (byte[])args[0];

                    if (!Runtime.CheckWitness(admin)) return false;
                    return removeBondAccount(address);
                }
                #region 升级合约,耗费990,仅限管理员
                if (operation == "upgrade")
                {
                    //不是管理员 不能操作
                    if (!Runtime.CheckWitness(admin))
                        return false;

                    if (args.Length != 1 && args.Length != 9)
                        return false;

                    byte[] script = Blockchain.GetContract(ExecutionEngine.ExecutingScriptHash).Script;
                    byte[] new_script = (byte[])args[0];
                    //如果传入的脚本一样 不继续操作
                    if (script == new_script)
                        return false;

                    byte[] parameter_list = new byte[] { 0x07, 0x10 };
                    byte return_type = 0x05;
                    bool need_storage = (bool)(object)07;
                    string name = "sar";
                    string version = "1";
                    string author = "alchemint";
                    string email = "0";
                    string description = "sar";

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

        private static bool claimFee(byte[] addr)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameters to and to SHOULD be 20-byte addresses.");

            //check state
            //if (!checkState(SAR_STATE))
            //    throw new InvalidOperationException("The sar state MUST not be pause.");

            var key = getSARKey(addr);
            byte[] bytes = getSAR4C(addr);
            if (bytes.Length == 0) throw new InvalidOperationException("The sar can not be null.");

            SARInfo sarInfo = Helper.Deserialize(bytes) as SARInfo;
            BigInteger sdsFee = sarInfo.sdsFee;
            if (sdsFee > 0) {
                byte[] from = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));
                byte[] sdsAsset = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));
                //从合约转普通地址
                var SDSDContract = (NEP5Contract)sdsAsset.ToDelegate();
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = addr;
                arg[2] = sdsFee;
                var nep5Contract = (NEP5Contract)sdsAsset.ToDelegate();
                if (!(bool)nep5Contract("transfer_contract", arg)) return false;
            }
            sarInfo.sdsFee = 0;
            Storage.Put(Storage.CurrentContext,key,Helper.Serialize(sarInfo));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            //notify
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_CLAIMFEE, sdsFee);
            return true;
        }

        private static bool migrateSAR4C(byte[] addr)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            //check SAR 
            if (checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST be pause.");

            //check SAR
            var key = getSARKey(addr);
            byte[] bytes = getSAR4C(addr);
            if (bytes.Length == 0) throw new InvalidOperationException("The sar can not be null.");

            SARInfo sarInfo = Helper.Deserialize(bytes) as SARInfo;

            BigInteger locked = sarInfo.locked;
            BigInteger hasDrawed = sarInfo.hasDrawed;
            BigInteger sdsFee = sarInfo.sdsFee;
            string assetType = sarInfo.assetType;

            if (sdsFee > 0) throw new InvalidOperationException("The sdsFee must not be 0.");

            byte[] nep5AssetID = Storage.Get(Storage.CurrentContext, getAccountKey(assetType.AsByteArray()));
            byte[] sdusdAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDUSD_ACCOUNT.AsByteArray()));
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] newSARID = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT_NEW.AsByteArray()));
            if (newSARID.Length == 0) return false;

            byte[] from = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));
            if (from.Length == 0) return false;

            {
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = newSARID;
                arg[2] = locked;
                var nep5Contract = (NEP5Contract)nep5AssetID.ToDelegate();
                if (!(bool)nep5Contract("transfer", arg)) return false;
            }
            //清算账户余额
            BigInteger rescue = getRescue(sarInfo.assetType,addr);
            {
                var newContract = (NEP5Contract)newSARID.ToDelegate();
                object[] args = new object[12];
                args[0] = addr;
                args[1] = sarInfo.txid;
                args[2] = sarInfo.locked;
                args[3] = sarInfo.hasDrawed;
                args[4] = sarInfo.assetType;
                args[5] = sarInfo.status;
                args[6] = sarInfo.bondLocked;
                args[7]= sarInfo.bondDrawed;
                args[8] = sarInfo.lastHeight;
                args[9]=  sarInfo.fee;
                args[10]= sarInfo.sdsFee;
                args[11] = rescue;
                if (!(bool)newContract("createSAR4C", args))return false;
            }
            Storage.Delete(Storage.CurrentContext, key);
            return true;
        }

        private static bool claimRescue(string assetType, byte[] addr)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameters to and to SHOULD be 20-byte addresses.");

            //check state
            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST not be pause.");

            byte[] rescueKey = getRescueKey(assetType.AsByteArray(), addr);
            BigInteger mount =  Storage.Get(Storage.CurrentContext, rescueKey).AsBigInteger();

            if(mount<=0)
                throw new InvalidOperationException("The mount is exception.");
            byte[] nep5AssetID = Storage.Get(Storage.CurrentContext, getAccountKey(assetType.AsByteArray()));

            //拿到该有的NEP5
            byte[] from = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));
            if (from.Length == 0) return false;
            {
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = addr;
                arg[2] = mount;
                var assetContract = (NEP5Contract)nep5AssetID.ToDelegate();

                if (!(bool)assetContract("transfer", arg)) return false;
            }

            return true;
        }

        private static BigInteger getRescue(string assetType, byte[] addr)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameters to and to SHOULD be 20-byte addresses.");

            byte[] rescueKey = getRescueKey(assetType.AsByteArray(), addr);
            return Storage.Get(Storage.CurrentContext,rescueKey).AsBigInteger();
        }

        private static bool migrateAsset(byte[] asset, byte[] to, BigInteger mount)
        {
            if(to.Length != 20)
                throw new InvalidOperationException("The parameters to and to SHOULD be 20-byte addresses.");

            if (mount <= 0)
                throw new InvalidOperationException("The parameter mount MUST be greater than 0.");

            byte[] from = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));

            BigInteger nep5Price = 0;
            {
                var AssetContract = (NEP5Contract)asset.ToDelegate();
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = to;
                arg[2] = mount;
                if(!(bool)AssetContract("transfer", arg))return false;
            }
            return true;

        }

        //checkState 1:normal  0:stop
        private static bool checkState(string configKey) {
            byte[] key = getConfigKey(configKey.AsByteArray());
            StorageMap config = Storage.CurrentContext.CreateMap(nameof(config));
            BigInteger value = config.Get(key).AsBigInteger();

            if (value == 1) return true;
            return false;
        }

        private static bool batchSAR4C(byte[] addr, SARInfo sar)
        {
            //SAR是否存在
            byte[] key = getSARKey(addr);

            byte[] sarCurr = Storage.Get(Storage.CurrentContext, key);
            if (sarCurr.Length > 0)
                return false;

            Storage.Put(Storage.CurrentContext,key,Helper.Serialize(sar));
            return true;
        }

        private static bool createSAR4C(byte[] addr, SARInfo sar)
        {
            //SAR是否存在
            byte[] key = getSARKey(addr);

            byte[] sarCurr = Storage.Get(Storage.CurrentContext, key);
            if (sarCurr.Length > 0)
                return false;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sar));
            return true;
        }

        public static bool setAccount(string key, byte[] address)
        {
            if (address.Length != 20)
                throw new InvalidOperationException("The parameters address and to SHOULD be 20-byte addresses.");

            Storage.Put(Storage.CurrentContext, getAccountKey(key.AsByteArray()), address);
            return true;
        }

        public static bool setBondAccount(byte[] address)
        {
            if (address.Length != 20) return false;
            BigInteger value = Storage.Get(Storage.CurrentContext,getBondKey(address)).AsBigInteger();

            if (value == 1) return false;
            Storage.Put(Storage.CurrentContext,getBondKey(address),1);
            return true;
        }

        public static bool removeBondAccount(byte[] address)
        {
            if (address.Length != 20) return false;
            Storage.Delete(Storage.CurrentContext, getBondKey(address));
            return true;
        }

        private static SARTransferDetail getSARTxInfo(byte[] txid)
        {
            byte[] v = Storage.Get(Storage.CurrentContext, txid);
            if (v.Length == 0)
                return null;
            return (SARTransferDetail)Helper.Deserialize(v);
        }

        private static byte[] getSAR4C(byte[] addr)
        {
            //SAR是否存在
            byte[] key = getSARKey(addr);
            return Storage.Get(Storage.CurrentContext, key);
        }

        private static Boolean rescue(byte[] otherAddr,byte[] addr,BigInteger mount)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (otherAddr.Length != 20)
                throw new InvalidOperationException("The parameter otherAddr SHOULD be 20-byte addresses.");

            if (mount <= 0)
                throw new InvalidOperationException("The parameter mount MUST be greater than 0.");

            if (otherAddr.AsBigInteger() == addr.AsBigInteger())
                throw new InvalidOperationException("The self can not do rescue.");

            //check state
            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST not be pause.");

            //check SAR
            var key = getSARKey(otherAddr);
            byte[] bytes = Storage.Get(Storage.CurrentContext, key);
            if (bytes.Length == 0) throw new InvalidOperationException("The sar can not be null.");

            var ownerKey = getSARKey(addr);
            byte[] ownerBytes = Storage.Get(Storage.CurrentContext, ownerKey);
            if (ownerBytes.Length == 0) throw new InvalidOperationException("The owner sar can not be null.");

            SARInfo sarInfo = Helper.Deserialize(bytes) as SARInfo;
            BigInteger lockedNeo = sarInfo.locked;
            BigInteger hasDrawed = sarInfo.hasDrawed;
            string assetType = sarInfo.assetType;

            byte[] nep5AssetID = Storage.Get(Storage.CurrentContext, getAccountKey(assetType.AsByteArray()));
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] sdusdAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDUSD_ACCOUNT.AsByteArray()));

            //不需要清算
            if (hasDrawed <= 0)
                throw new InvalidOperationException("The param is exception.");

            if (mount > hasDrawed)
                throw new InvalidOperationException("The param is exception.");


            //当前NEP5美元价格，需要从价格中心获取
            BigInteger nep5Price = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = assetType;
                nep5Price = (BigInteger)OracleContract("getTypeB", arg);
            }

            //当前兑换率，需要从配置中心获取
            BigInteger rate = 0;
          
            //当前清算折扣比例，需要从配置中心获取
            BigInteger rateClear = 0;
           
            //清算最低兑换率，需要从配置中心获取
            BigInteger rescueRate = 0;
          
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = assetType;
                Config config = (Config)OracleContract("getStructConfig", arg);
                rate = config.liquidate_line_rate_c;
                rateClear = config.liquidate_dis_rate_c;
                rescueRate = config.liquidate_top_rate_c;
            }
            //计算是否需要清算 乘以10000的值 如1.5 => 15000
            BigInteger currentRate = lockedNeo * nep5Price / (hasDrawed * 10000);
            if (currentRate > rate * 100)
                throw new InvalidOperationException("The param is exception.");

            //计算可以拿到的SNEO资产
            BigInteger canClearNeo = mount* TEN_POWER / (nep5Price * rateClear);

            if (canClearNeo <= 0)
                throw new InvalidOperationException("The param is exception.");

            if (canClearNeo > lockedNeo)
                throw new InvalidOperationException("The param is exception.");

            //清算部分后的抵押率 如：160
            BigInteger lastRate = (lockedNeo - canClearNeo) * nep5Price / ((hasDrawed - mount) * SIX_POWER);     

            if (lastRate > rescueRate)
                throw new InvalidOperationException("The param is exception.");

            if (lastRate <= 100) {
                if(mount != hasDrawed)
                    throw new InvalidOperationException("The param is exception.");
            }
            //销毁等量SDUSD
            {
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[2];
                arg[0] = addr;
                arg[1] = mount;
                if(!(bool)SDUSDContract("destory", arg))
                    throw new InvalidOperationException("The destory is exception.");
            }

            sarInfo.locked = lockedNeo - canClearNeo;
            sarInfo.hasDrawed = hasDrawed - mount;
            Storage.Put(Storage.CurrentContext, key,Helper.Serialize(sarInfo));

            //记录清算数据
            SARInfo ownerSarInfo = Helper.Deserialize(ownerBytes) as SARInfo;
            BigInteger ownerLock = ownerSarInfo.locked;
            ownerSarInfo.locked = ownerLock + canClearNeo;

            Storage.Put(Storage.CurrentContext,ownerKey,Helper.Serialize(ownerSarInfo));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            //触发操作事件
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_RESUCE, mount);
            return true;
        }

        //Bond 
        private static Boolean rescueT(byte[] otherAddr, byte[] addr, BigInteger mount)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (otherAddr.Length != 20)
                throw new InvalidOperationException("The parameter otherAddr SHOULD be 20-byte addresses.");

            if (mount <= 0)
                throw new InvalidOperationException("The parameter mount MUST be greater than 0.");

            BigInteger state = Storage.Get(Storage.CurrentContext,getBondKey(addr)).AsBigInteger();

            if (state != 1)
                throw new InvalidOperationException("The state is exception.");

            //SAR是否存在
            byte[] key = getSARKey(otherAddr);

            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0)
                throw new InvalidOperationException("The sar is not exist.");

            SARInfo sarInfo = (SARInfo)Helper.Deserialize(sar);

            BigInteger lockedPneo = sarInfo.locked;
            BigInteger hasDrawed = sarInfo.hasDrawed;
            string assetType = sarInfo.assetType;

            byte[] nep5AssetID = Storage.Get(Storage.CurrentContext, getAccountKey(assetType.AsByteArray()));
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] sdusdAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDUSD_ACCOUNT.AsByteArray()));
            //不需要清算
            if (hasDrawed <= 0)
                throw new InvalidOperationException("The param is exception.");

            if (mount > hasDrawed)
                throw new InvalidOperationException("The param is exception.");

            //当前NEP5美元价格，需要从价格中心获取
            BigInteger nep5Price = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = assetType;
                nep5Price = (BigInteger)OracleContract("getTypeB", arg);
            }

            //当前兑换率，需要从配置中心获取
            BigInteger rate = 0;

            //当前清算折扣比例，需要从配置中心获取
            BigInteger rateClear = 0;

            //清算最低兑换率，需要从配置中心获取
            BigInteger rescueRate = 0;

            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = assetType;
                Config config = (Config)OracleContract("getStructConfig", arg);
                rate = config.liquidate_line_rateT_c;
                rateClear = config.liquidate_dis_rate_c;
                rescueRate = config.liquidate_top_rate_c;
            }

            //计算是否需要清算 乘以10000的值 如1.2 => 12000
            BigInteger currentRate = lockedPneo * nep5Price / (hasDrawed * 10000);
            if (currentRate > rate * 100)
                throw new InvalidOperationException("The param is exception.");

            //计算可以拿到的SNEO资产
            BigInteger canClearPneo = mount * TEN_POWER / (nep5Price * rateClear);

            if (canClearPneo > lockedPneo)
                throw new InvalidOperationException("The param is exception.");

            //清算部分后的抵押率 如：160
            BigInteger lastRate = (lockedPneo - canClearPneo) * nep5Price / ((hasDrawed - mount) * SIX_POWER);

            if (lastRate > rescueRate)
                throw new InvalidOperationException("The param is exception.");

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            //真正开始Bond操作
            //当前地址SAR是否存在
            byte[] currKey = getSARKey(addr);

            byte[] currSar = Storage.Get(Storage.CurrentContext, currKey);
            SARInfo currSarInfo = null;
            if (currSar.Length == 0)
            {
                currSarInfo = new SARInfo();
                currSarInfo.owner = addr;
                currSarInfo.txid = txid;
                currSarInfo.hasDrawed = 0;
                currSarInfo.locked = 0;
                currSarInfo.status = 1;
                currSarInfo.assetType = sarInfo.assetType;
                currSarInfo.bondLocked = canClearPneo;
                currSarInfo.bondDrawed = mount;
                //触发操作事件
                Operated(addr, currSarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_OPEN,0);
            }
            else
            {
                currSarInfo = (SARInfo)Helper.Deserialize(currSar);
                BigInteger bondLocked = currSarInfo.bondLocked;
                BigInteger bondDrawed = currSarInfo.bondDrawed;

                currSarInfo.bondLocked = bondLocked + canClearPneo;
                currSarInfo.bondDrawed = bondDrawed + mount;
            }
            Storage.Put(Storage.CurrentContext, currKey, Helper.Serialize(currSarInfo));

            sarInfo.locked = lockedPneo - canClearPneo;
            sarInfo.hasDrawed = hasDrawed - mount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            //触发操作事件
            Operated(addr, currSarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_BOND_RESUCE, mount);
            return true;
        }

        private static BigInteger getConfig(string configKey)
        {
            byte[] key = getConfigKey(configKey.AsByteArray());
            StorageMap config = Storage.CurrentContext.CreateMap(nameof(config));

            return config.Get(key).AsBigInteger();
        }

        private static Boolean setConfig(string configKey, BigInteger value)
        {
            //只允许超管操作
            if (!Runtime.CheckWitness(admin)) return false;

            byte[] key = getConfigKey(configKey.AsByteArray());
            StorageMap config = Storage.CurrentContext.CreateMap(nameof(config));
            config.Put(key, value);
            return true;
        }

        private static Boolean contract(byte[] addr, BigInteger mount)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (mount <= 0)
                throw new InvalidOperationException("The parameter mount MUST be greater than 0.");

            //check SAR
            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST not be pause.");

            //check SAR
            var key = getSARKey(addr);
            byte[] bytes = getSAR4C(addr);
            if (bytes.Length == 0) throw new InvalidOperationException("The sar can not be null.");

            SARInfo sarInfo = Helper.Deserialize(bytes) as SARInfo;

            BigInteger locked = sarInfo.locked;
            BigInteger hasDrawed = sarInfo.hasDrawed;

            //SD赎回量要小于已经在仓的
            if (mount > hasDrawed)
                throw new InvalidOperationException("The param is exception.");

            byte[] sdusdAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDUSD_ACCOUNT.AsByteArray()));
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] sdsAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));
            byte[] to = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));

            //get asset price
            BigInteger sdsPrice = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = CONFIG_PRICE_SDS;
                sdsPrice = (BigInteger)OracleContract("getTypeB", arg);
            }

            //乘以10的8次方后结果=》148  年化13，15秒的利率
            BigInteger fee_rate = 148;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = CONFIG_FEE_C;
                fee_rate = (BigInteger)OracleContract("getTypeA", arg);
            }

            //cal fee
            uint blockHeight = Blockchain.GetHeight();
            BigInteger fee = sarInfo.fee;
            BigInteger sdsFree = sarInfo.sdsFee;

            //有债仓,根据全量计算
            uint lastHeight = sarInfo.lastHeight;
            BigInteger currFee = (blockHeight - lastHeight) * hasDrawed * fee_rate / sdsPrice;

            //需要收取的手续费
            BigInteger needFee = mount * currFee/ hasDrawed;

            //手续费不够扣
            if (needFee > sdsFree)
                throw new InvalidOperationException("The param is exception.");

            //减少金额
            {
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[2];
                arg[0] = addr;
                arg[1] = mount;
                if (!(bool)SDUSDContract("destory", arg)) return false;
            }

            sarInfo.lastHeight = blockHeight;
            sarInfo.fee = currFee - needFee;
            sarInfo.sdsFee = sdsFree - needFee;
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

        private static Boolean withdraw(byte[] addr, BigInteger mount)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (mount <= 0)
                throw new InvalidOperationException("The parameter mount MUST be greater than 0.");

            //check SAR
            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST not be pause.");

            //check SAR
            var key = getSARKey(addr);
            byte[] bytes = getSAR4C(addr);
            if (bytes.Length == 0) throw new InvalidOperationException("The sar can not be null.");

            SARInfo sarInfo = Helper.Deserialize(bytes) as SARInfo;

            BigInteger locked = sarInfo.locked;
            BigInteger hasDrawed = sarInfo.hasDrawed;
            string assetType = sarInfo.assetType;

            byte[] nep5AssetID = Storage.Get(Storage.CurrentContext, getAccountKey(assetType.AsByteArray()));
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));

            //当前NEP5美元价格，需要从价格中心获取
            BigInteger nep5Price = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = assetType;
                nep5Price = (BigInteger)OracleContract("getTypeB", arg);
            }

            //当前兑换率，需要从配置中心获取
            BigInteger rate = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = CONFIG_RATE_C;
                rate = (BigInteger)OracleContract("getTypeA", arg);
            }

            //计算已经兑换过的PNEO量
            BigInteger hasDrawPNeo = hasDrawed * rate * SIX_POWER / nep5Price;

            //释放的总量大于已经剩余，不能操作
            if (mount > locked - hasDrawPNeo)
                throw new InvalidOperationException("The param is exception.");

            byte[] from = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));
            if (from.Length == 0)
                throw new InvalidOperationException("The param is exception.");
            {
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = addr;
                arg[2] = mount;
                var nep5Contract = (NEP5Contract)nep5AssetID.ToDelegate();

                if (!(bool)nep5Contract("transfer", arg)) return false;
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


        private static Boolean withdrawT(byte[] addr, BigInteger mount)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (mount <= 0)
                throw new InvalidOperationException("The parameter mount MUST be greater than 0.");

            //SAR是否存在
            byte[] key = getSARKey(addr);

            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0)
                throw new InvalidOperationException("The param is exception.");
            SARInfo sarInfo = (SARInfo)Helper.Deserialize(sar);

            BigInteger locked = sarInfo.bondLocked;
            BigInteger hasDrawed = sarInfo.bondDrawed;

            string assetType = sarInfo.assetType;

            byte[] nep5AssetID = Storage.Get(Storage.CurrentContext, getAccountKey(assetType.AsByteArray()));
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] sdusdAssetID = Storage.Get(Storage.CurrentContext,getAccountKey(SDUSD_ACCOUNT.AsByteArray()));
            byte[] from = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));
            if (from.Length == 0)
                throw new InvalidOperationException("The param is exception.");

            //释放的总量大于已经剩余，不能操作
            if (mount > locked)
                throw new InvalidOperationException("The param is exception.");

            //需要消耗的SDUSD
            BigInteger needConsume = hasDrawed * mount / locked;
            {
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[2];
                arg[0] = addr;
                arg[1] = needConsume;
                if (!(bool)SDUSDContract("destory", arg)) return false;
            }

            {
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = addr;
                arg[2] = mount;
                var nep5Contract = (NEP5Contract)nep5AssetID.ToDelegate();
                if (!(bool)nep5Contract("transfer", arg)) return false;
            }

            //重新设置锁定量
            sarInfo.bondLocked = locked - mount;
            sarInfo.bondDrawed = hasDrawed - needConsume;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            //记录交易详细数据
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = sarInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_BOND_WITHDRAW;
            detail.operated = mount;
            detail.hasLocked = locked - mount;
            detail.hasDrawed = hasDrawed;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_BOND_WITHDRAW, mount);
            return true;
        }

        private static Boolean openSAR4C(byte[] addr,string assetType)
        {
            //已经有SAR就不重新建
            var key = getSARKey(addr);
            byte[] bytes = getSAR4C(addr);
            if (bytes.Length > 0) throw new InvalidOperationException("The sar can  be null.");

            //交易ID 
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            //交易信息
            SARInfo sarInfo = new SARInfo();
            sarInfo.owner = addr;
            sarInfo.locked = 0;
            sarInfo.hasDrawed = 0;
            sarInfo.txid = txid;
            sarInfo.assetType = assetType;
            sarInfo.status = 1;
            sarInfo.bondLocked = 0;
            sarInfo.bondDrawed = 0;

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

        private static Boolean reserve(byte[] addr, BigInteger lockMount)
        {
            if(addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (lockMount <= 0) 
                throw new InvalidOperationException("The parameter lockMount MUST be greater than 0.");

            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST not be pause.");

            var key = getSARKey(addr);
            byte[] bytes = getSAR4C(addr);
            if (bytes.Length == 0) throw new InvalidOperationException("The sar can  be null.");

            SARInfo sarInfo = Helper.Deserialize(bytes) as SARInfo;

            //assetType 
            string assetType =  sarInfo.assetType;
            //get nep5 asset
            byte[] nep5AssetID = Storage.Get(Storage.CurrentContext, getAccountKey(assetType.AsByteArray()));

            //current account
            byte[] to = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));
            if (to.Length == 0)
                throw new InvalidOperationException("The parameter to SHOULD be greater than 0.");

            object[] arg = new object[3];
            arg[0] = addr;
            arg[1] = to;
            arg[2] = lockMount;

            var AssetContract = (NEP5Contract)nep5AssetID.ToDelegate();

            if (!(bool)AssetContract("transfer", arg))
                throw new InvalidOperationException("The operation is exception.");

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

        private static Boolean recharge(byte[] addr, BigInteger mount)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (mount <= 0)
                throw new InvalidOperationException("The parameter mount MUST be greater than 0.");

            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST not be pause.");

            var key = getSARKey(addr);
            byte[] bytes = getSAR4C(addr);
            if (bytes.Length == 0) throw new InvalidOperationException("The sar can not be null.");

            SARInfo sarInfo = Helper.Deserialize(bytes) as SARInfo;

            //get nep5 asset
            byte[] sdsAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));

            //current account
            byte[] to = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));
            if (to.Length == 0)
                throw new InvalidOperationException("The parameter to SHOULD be greater than 0.");

            object[] arg = new object[3];
            arg[0] = addr;
            arg[1] = to;
            arg[2] = mount;

            var AssetContract = (NEP5Contract)sdsAssetID.ToDelegate();

            if (!(bool)AssetContract("transfer", arg))
                throw new InvalidOperationException("The operation is exception.");

            //设置充值的SDS数量
            BigInteger sdsFee = sarInfo.sdsFee;
            sarInfo.sdsFee = sdsFee + mount;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            //记录交易详细数据
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = sarInfo.txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_RECHARGE;
            detail.operated = mount;
            detail.hasLocked = sarInfo.locked;
            detail.hasDrawed = sarInfo.hasDrawed;
            detail.txid = txid;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //notify
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_RECHARGE, mount);
            return true;
        }

        private static Boolean expande(byte[] addr, BigInteger drawMount)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (drawMount <= 0)
                throw new InvalidOperationException("The parameter drawMount MUST be greater than 0.");

            //check SAR
            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST not be pause.");

            //check SAR
            var key = getSARKey(addr);
            byte[] bytes = getSAR4C(addr);
            if (bytes.Length == 0)
                throw new InvalidOperationException("The sar can not be null.");

            SARInfo sarInfo = Helper.Deserialize(bytes) as SARInfo;

            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] sdusdAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDUSD_ACCOUNT.AsByteArray()));

            BigInteger locked = sarInfo.locked;
            BigInteger hasDrawed = sarInfo.hasDrawed;
            string assetType = sarInfo.assetType;

            //get asset price
            BigInteger assetPrice = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = assetType;
                assetPrice = (BigInteger)OracleContract("getTypeB", arg);
            }

            BigInteger rate = 0;
            BigInteger releaseMax = 0;
            BigInteger fee_rate = 0;

            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = assetType;
                Config config = (Config)OracleContract("getStructConfig", arg);
                rate = config.liquidate_line_rate_c;
                releaseMax = config.debt_top_c;
                fee_rate = config.fee_rate_c;
            }

            //calculate max count
            BigInteger allSd = locked * assetPrice / (rate * SIX_POWER);
            
            //check can draw count
            if (allSd < hasDrawed + drawMount)
                throw new InvalidOperationException("The sar can draw larger than max.");


            //get totalSupply
            BigInteger totalSupply = 0;
            {
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[2];
                arg[0] = addr;
                arg[1] = drawMount;
                totalSupply = (BigInteger)SDUSDContract("totalSupply", arg);
            }

            //check release max count
            if (totalSupply + drawMount > releaseMax)
                throw new InvalidOperationException("The sar can draw larger than releaseMax.");

            //increase sdusd
            {
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[2];
                arg[0] = addr;
                arg[1] = drawMount;
                if (!(bool)SDUSDContract("increase", arg))
                    throw new InvalidOperationException("The sdusd increased error.");
            }

            //get asset price
            BigInteger sdsPrice = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = CONFIG_PRICE_SDS;
                sdsPrice = (BigInteger)OracleContract("getTypeB", arg);
            }

            //cal fee
            uint blockHeight = Blockchain.GetHeight();
            BigInteger fee = sarInfo.fee;
           
            //无债仓不计算费用，记录区块
            if (hasDrawed == 0)
            {
                sarInfo.lastHeight = blockHeight;
                sarInfo.fee = 0;

            }else {
                //有债仓,根据全量计算
                uint lastHeight = sarInfo.lastHeight;
                BigInteger currFee = (blockHeight - lastHeight) * hasDrawed * fee_rate / sdsPrice;

                sarInfo.lastHeight = blockHeight;
                sarInfo.fee = currFee + fee;
            }
            sarInfo.hasDrawed = hasDrawed + drawMount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            //record detail
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

            //notify
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_DRAW, drawMount);
            return true;

        }

        private static Boolean close(byte[] addr)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            //check SAR
            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST not be pause.");

            var key = getSARKey(addr);
            byte[] bytes = getSAR4C(addr);
            if (bytes.Length == 0) throw new InvalidOperationException("The sar can not be null.");

            SARInfo sarInfo = Helper.Deserialize(bytes) as SARInfo;

            BigInteger locked = sarInfo.locked;
            BigInteger hasDrawed = sarInfo.hasDrawed;
            string assetType = sarInfo.assetType;

            if (hasDrawed > 0) throw new InvalidOperationException("The expand count must be 0.");

            //delete sar
            Storage.Delete(Storage.CurrentContext, key);

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            //record detail
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = sarInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_SHUT;
            detail.operated = hasDrawed;
            detail.hasLocked = locked;
            detail.hasDrawed = hasDrawed;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //notify
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_SHUT, 0);
            return true;
        }

        public class SARInfo
        {

            //创建者
            public byte[] owner;

            //交易序号
            public byte[] txid;

            //被锁定的资产,如SNEO
            public BigInteger locked;

            //已经提取的资产，如SDUSDT  
            public BigInteger hasDrawed;

            //neo:neo_price   gas:gas_price 
            public string assetType;

            //1安全  2不安全 3不可用   
            public int status;

            //Bond锁定的资产
            public BigInteger bondLocked;

            //Bond锁定的SDUSD
            public BigInteger bondDrawed;

            //最新expande操作的区块高度
            public uint lastHeight;

            //手续费总量 sds
            public BigInteger fee;

            //充值的SDS手续费
            public BigInteger sdsFee;
           
        }

        public class BondSAR
        {
            //创建者
            public byte[] owner;

            //交易序号
            public byte[] txid;


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

        public class Config
        {
            //B端抵押率   50
            public BigInteger liquidate_line_rate_b;

            //C端抵押率  150
            public BigInteger liquidate_line_rate_c;

            //C端清算折扣  90
            public BigInteger liquidate_dis_rate_c;

            //C端费用率  15秒的费率 乘以10的8次方  148
            public BigInteger fee_rate_c;

            //C端最高可清算抵押率  160
            public BigInteger liquidate_top_rate_c;

            //C端伺机者可清算抵押率 120
            public BigInteger liquidate_line_rateT_c;

            //C端发行费用 1
            public BigInteger issuing_fee_c;

            //B端发行费用  1000000000
            public BigInteger issuing_fee_b;

            //C端最大发行量(债务上限)  1000000000000
            public BigInteger debt_top_c;

        }
    }
}

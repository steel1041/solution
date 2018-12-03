using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework.Services.System;

namespace SAR4C
{
    public class SAR4C : SmartContract
    {

        /** Operation of SAR records
         * addr,sartxid,txid,type,operated*/
        [DisplayName("sarOperator4C")]
        public static event Action<byte[], byte[], byte[], BigInteger, BigInteger> Operated;

        /** Fee records
         * addr,sartxid,txid,feeSDUSD,feeSDS*/
        [DisplayName("feeOperator4C")]
        public static event Action<byte[], byte[], byte[], BigInteger, BigInteger> FeeOperated;

        public delegate object NEP5Contract(string method, object[] args);

        //Default multiple signature committee account
        private static readonly byte[] committee = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");


        /** 
         * Static param
         */
        //price of SDS
        private const string CONFIG_PRICE_SDS = "sds_price";
        //risk management
        private const string CONFIG_CLEAR_RATE = "liquidate_dis_rate_c";
        private const string CONFIG_RATE_C = "liquidate_line_rate_c";
        private const string CONFIG_FEE_C = "fee_rate_c";
        private const string CONFIG_BOND_C = "liquidate_line_rateT_c";
        private const string CONFIG_RELEASE_MAX = "debt_top_c";
        //for upgrade
        private const string STORAGE_ACCOUNT_NEW = "storage_account_new";
        private const string STORAGE_ACCOUNT_OLD = "storage_account_old";

        //system account
        private const string SDS_ACCOUNT = "sds_account";
        private const string ADMIN_ACCOUNT = "admin_account";
        private const string ORACLE_ACCOUNT = "oracle_account";
        private const string SASSET_ACCOUNT = "sasset_account";
        private const string SDUSD_ACCOUNT = "sdusd_account";

        private const string SAR_STATE = "sar_state";
        private const string BOND_ISSUED_GLOBAL = "bondIssuedGlobal";

        private const ulong SIX_POWER = 1000000;
        private const ulong TEN_POWER = 10000000000;
        private const ulong EIGHT_POWER = 100000000;
        private const ulong SIXTEEN_POWER = 10000000000000000;

        /*     
        * Key wrapper
        */
        private static byte[] getSARKey(byte[] addr) => new byte[] { 0x12 }.Concat(addr);
        private static byte[] getTxidKey(byte[] txid) => new byte[] { 0x14 }.Concat(txid);
        private static byte[] getAccountKey(byte[] account) => new byte[] { 0x15 }.Concat(account);
        private static byte[] getBondKey(byte[] account) => new byte[] { 0x16 }.Concat(account);
        private static byte[] getConfigKey(byte[] key) => new byte[] { 0x17 }.Concat(key);
        private static byte[] getRescueKey(byte[] assetType, byte[] addr) => new byte[] { 0x18 }.Concat(assetType).Concat(addr);
        private static byte[] getBondGlobalKey(byte[] key) => new byte[] { 0x19 }.Concat(key);

        //Transaction type
        public enum ConfigTranType
        {
            TRANSACTION_TYPE_OPEN = 1,
            TRANSACTION_TYPE_LOCK,
            TRANSACTION_TYPE_DRAW,
            TRANSACTION_TYPE_FREE,
            TRANSACTION_TYPE_WIPE,
            TRANSACTION_TYPE_SHUT,
            TRANSACTION_TYPE_RESUCE,
            TRANSACTION_TYPE_GIVE,
            TRANSACTION_TYPE_BOND_RESUCE,
            TRANSACTION_TYPE_BOND_WITHDRAW,
            TRANSACTION_TYPE_RECHARGE,
            TRANSACTION_TYPE_CLAIMRESCUE,
            TRANSACTION_TYPE_CLAIMFEE
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
            var magicstr = "2018-11-21 14:40:10";

            if (Runtime.Trigger == TriggerType.Verification)
            {
                return false;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {

                var callscript = ExecutionEngine.CallingScriptHash;

                if (operation == "openSAR4C")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    string assetType = (string)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return openSAR4C(addr, assetType);
                }
                /**An example of upgrade 'accept' method, the createSAR4C interface should been 
                *  implemented in the following new SAR4C contract
                */
                if (operation == "createSAR4C")
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

                    //Only the legal SAR4C (old version) can call this method
                    byte[] account = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT_OLD.AsByteArray()));
                    if (account.AsBigInteger() != callscript.AsBigInteger()) return false;
                    return createSAR4C(addr, sar);
                }

                //Migrate SAR account to new contract by owner of SAR
                if (operation == "migrateSAR4C")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return migrateSAR4C(addr);
                }

                //Forcely migrate by anyone if the SAR could be liquidated
                if (operation == "forceMigrate")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];

                    return forceMigrate(addr);
                }

                if (operation == "getSAR4C")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];

                    byte[] sarInfo = getSAR4C(addr);
                    if (sarInfo.Length == 0)
                        return null;
                    return Helper.Deserialize(sarInfo) as SARInfo;
                }

                if (operation == "getSARTxInfo")
                {
                    if (args.Length != 1) return false;
                    byte[] txid = (byte[])args[0];
                    return getSARTxInfo(txid);
                }

                //locked nep5 asset to SAR
                if (operation == "reserve")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    //NEP5 Asset mount 
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return reserve(addr, mount);
                }

                //locked SDS asset
                if (operation == "recharge")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    //SDS Asset mount 
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return recharge(addr, mount);
                }

                //issue SDUSD
                if (operation == "expande")
                {
                    if (args.Length != 2) return false;

                    byte[] addr = (byte[])args[0];
                    //SDUSD
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return expande(addr, mount);
                }

                //get asset from SAR
                if (operation == "withdraw")
                {
                    if (args.Length != 2) return false;

                    byte[] addr = (byte[])args[0];
                    //NEP5 asset
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return withdraw(addr, mount);
                }

                //get asset from SAR by qualified Alchemist offseting bond 
                if (operation == "withdrawT")
                {
                    if (args.Length != 2) return false;

                    byte[] addr = (byte[])args[0];
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return withdrawT(addr, mount);
                }

                //payback sdusd 
                if (operation == "contract")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger mount = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return contract(addr, mount);
                }

                //close a SAR
                if (operation == "close")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return close(addr);
                }

                //liquidate a unsafe SAR 
                if (operation == "rescue")
                {
                    if (args.Length != 3) return false;
                    byte[] otherAddr = (byte[])args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger mount = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return rescue(otherAddr, addr, mount);
                }

                if (operation == "getBondGlobal")
                {
                    return getBondGlobal();
                }

                //get back SDS
                if (operation == "claimFee")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    BigInteger mount = (BigInteger)args[1];
                    if (!Runtime.CheckWitness(addr)) return false;

                    return claimFee(addr, mount);
                }

                //get back all SDS
                if (operation == "claimAllFee")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];
                    if (!Runtime.CheckWitness(addr)) return false;
                    return claimAllFee(addr);
                }

                //liquidate a unsafe SAR using bond by qualified Alchemist
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
                    //only committee account
                    if (!checkAdmin()) return false;
                    return setAccount(key, address);
                }

                //Authorize a qualified Alchemist account
                if (operation == "setBondAccount")
                {
                    if (args.Length != 1) return false;
                    byte[] address = (byte[])args[0];

                    if (!checkAdmin()) return false;
                    return setBondAccount(address);
                }

                //remove a qualified Alchemist account
                if (operation == "removeBondAccount")
                {
                    if (args.Length != 1) return false;
                    byte[] address = (byte[])args[0];
                    //only committee account
                    if (!checkAdmin()) return false;
                    return removeBondAccount(address);
                }

                if (operation == "setConfig")
                {
                    if (args.Length != 2) return false;
                    string key = (string)args[0];
                    BigInteger value = (BigInteger)args[1];

                    if (!checkAdmin()) return false;
                    return setConfig(key, value);
                }

                if (operation == "getConfig")
                {
                    if (args.Length != 1) return false;
                    string key = (string)args[0];

                    return getConfig(key);
                }
                #region contract upgrade
                if (operation == "upgrade")
                {
                    //only committee account
                    if (!checkAdmin()) return false;

                    if (args.Length != 1 && args.Length != 9)
                        return false;

                    byte[] script = Blockchain.GetContract(ExecutionEngine.ExecutingScriptHash).Script;
                    byte[] new_script = (byte[])args[0];
                    //new script should different from old script
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

        private static BigInteger getBondGlobal()
        {
            byte[] bondKey = getBondGlobalKey(BOND_ISSUED_GLOBAL.AsByteArray());
            BigInteger total = Storage.Get(Storage.CurrentContext, bondKey).AsBigInteger();
            return total;
        }

        private static bool claimAllFee(byte[] addr)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameters to and to SHOULD be 20-byte addresses.");

            var key = getSARKey(addr);
            byte[] bytes = getSAR4C(addr);
            if (bytes.Length == 0)
                throw new InvalidOperationException("The sar can not be null.");

            SARInfo sarInfo = Helper.Deserialize(bytes) as SARInfo;
            BigInteger sdsFee = sarInfo.sdsFee;
            if (sdsFee > 0)
            {
                byte[] from = ExecutionEngine.ExecutingScriptHash;
                byte[] sdsAsset = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));
                var SDSDContract = (NEP5Contract)sdsAsset.ToDelegate();
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = addr;
                arg[2] = sdsFee;
                var nep5Contract = (NEP5Contract)sdsAsset.ToDelegate();
                if (!(bool)nep5Contract("transfer_contract", arg))
                    throw new InvalidOperationException("The transfer is exception.");
            }
            else
            {
                return false;
            }
            sarInfo.sdsFee = 0;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            //notify
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_CLAIMFEE, sdsFee);
            return true;
        }

        private static bool claimFee(byte[] addr, BigInteger mount)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameters to and to SHOULD be 20-byte addresses.");

            if (mount <= 0)
                throw new InvalidOperationException("The parameter mount MUST be greater than 0.");


            var key = getSARKey(addr);
            byte[] bytes = getSAR4C(addr);
            if (bytes.Length == 0)
                throw new InvalidOperationException("The sar can not be null.");

            SARInfo sarInfo = Helper.Deserialize(bytes) as SARInfo;
            BigInteger sdsFee = sarInfo.sdsFee;

            if (sdsFee <= 0) return false;

            if (mount > sdsFee) throw new InvalidOperationException("The operation is exception.");

            if (sdsFee > 0)
            {
                byte[] from = ExecutionEngine.ExecutingScriptHash;
                byte[] sdsAsset = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));
                var SDSDContract = (NEP5Contract)sdsAsset.ToDelegate();
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = addr;
                arg[2] = mount;
                var nep5Contract = (NEP5Contract)sdsAsset.ToDelegate();
                if (!(bool)nep5Contract("transfer_contract", arg))
                    throw new InvalidOperationException("The transfer is exception.");
            }
            sarInfo.sdsFee = sdsFee - mount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            //notify
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_CLAIMFEE, mount);
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

            byte[] nep5AssetID = Storage.Get(Storage.CurrentContext, getAccountKey(assetType.AsByteArray()));
            byte[] sdusdAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDUSD_ACCOUNT.AsByteArray()));
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] newSARID = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT_NEW.AsByteArray()));
            if (newSARID.Length == 0) return false;

            byte[] from = ExecutionEngine.ExecutingScriptHash;
            {
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = newSARID;
                arg[2] = locked;
                var nep5Contract = (NEP5Contract)nep5AssetID.ToDelegate();
                if (!(bool)nep5Contract("transfer", arg))
                    throw new InvalidOperationException("The operation is error.");
            }

            {
                var newContract = (NEP5Contract)newSARID.ToDelegate();
                object[] args = new object[11];
                args[0] = addr;
                args[1] = sarInfo.txid;
                args[2] = sarInfo.locked;
                args[3] = sarInfo.hasDrawed;
                args[4] = sarInfo.assetType;
                args[5] = sarInfo.status;
                args[6] = sarInfo.bondLocked;
                args[7] = sarInfo.bondDrawed;
                args[8] = sarInfo.lastHeight;
                args[9] = sarInfo.fee;
                args[10] = sarInfo.sdsFee;

                if (!(bool)newContract("createSAR4C", args))
                    throw new InvalidOperationException("The operation is error.");
            }
            return true;
        }

        private static bool forceMigrate(byte[] addr)
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

            byte[] nep5AssetID = Storage.Get(Storage.CurrentContext, getAccountKey(assetType.AsByteArray()));
            byte[] sdusdAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDUSD_ACCOUNT.AsByteArray()));
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] newSARID = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT_NEW.AsByteArray()));
            if (newSARID.Length == 0) throw new InvalidOperationException("The param is exception.");

            byte[] from = ExecutionEngine.ExecutingScriptHash;

            BigInteger nep5Price = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = assetType;
                nep5Price = (BigInteger)OracleContract("getTypeB", arg);
            }

            BigInteger rate = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = assetType;
                Config config = (Config)OracleContract("getStructConfig", arg);
                rate = config.liquidate_line_rate_c;
            }

            BigInteger currentRate = locked * nep5Price / (hasDrawed * 10000);
            if (currentRate >= rate * 100)
                throw new InvalidOperationException("The param is exception.");

            {
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = newSARID;
                arg[2] = locked;
                var nep5Contract = (NEP5Contract)nep5AssetID.ToDelegate();
                if (!(bool)nep5Contract("transfer", arg))
                    throw new InvalidOperationException("The operation is error.");
            }

            {
                var newContract = (NEP5Contract)newSARID.ToDelegate();
                object[] args = new object[11];
                args[0] = addr;
                args[1] = sarInfo.txid;
                args[2] = sarInfo.locked;
                args[3] = sarInfo.hasDrawed;
                args[4] = sarInfo.assetType;
                args[5] = sarInfo.status;
                args[6] = sarInfo.bondLocked;
                args[7] = sarInfo.bondDrawed;
                args[8] = sarInfo.lastHeight;
                args[9] = sarInfo.fee;
                args[10] = sarInfo.sdsFee;
                if (!(bool)newContract("createSAR4C", args))
                    throw new InvalidOperationException("The operation is error.");
            }
            return true;
        }


        //checkState 1:normal  0:stop
        private static bool checkState(string configKey)
        {
            byte[] key = getConfigKey(configKey.AsByteArray());
            BigInteger value = Storage.Get(Storage.CurrentContext, key).AsBigInteger();

            if (value == 1) return true;
            return false;
        }

        private static bool createSAR4C(byte[] addr, SARInfo sar)
        {
            byte[] key = getSARKey(addr);

            byte[] sarCurr = Storage.Get(Storage.CurrentContext, key);
            if (sarCurr.Length > 0)
                return false;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sar));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            //notify
            Operated(addr, txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_OPEN, 0);
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
            BigInteger value = Storage.Get(Storage.CurrentContext, getBondKey(address)).AsBigInteger();

            if (value == 1) return false;
            Storage.Put(Storage.CurrentContext, getBondKey(address), 1);
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
            byte[] v = Storage.Get(Storage.CurrentContext, getTxidKey(txid));
            if (v.Length == 0)
                return new SARTransferDetail();
            return (SARTransferDetail)Helper.Deserialize(v);
        }

        private static byte[] getSAR4C(byte[] addr)
        {
            byte[] key = getSARKey(addr);
            return Storage.Get(Storage.CurrentContext, key);
        }

        private static Boolean rescue(byte[] otherAddr, byte[] addr, BigInteger mount)
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

            if (hasDrawed <= 0)
                throw new InvalidOperationException("The param is exception.");

            if (mount > hasDrawed)
                throw new InvalidOperationException("The param is exception.");

            BigInteger nep5Price = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = assetType;
                nep5Price = (BigInteger)OracleContract("getTypeB", arg);
            }

            BigInteger rate = 0;
            BigInteger rateClear = 0;
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

            BigInteger currentRate = lockedNeo * nep5Price / (hasDrawed * 10000);

            if (currentRate >= rate * 100)
                throw new InvalidOperationException("The param is exception.");

            BigInteger canClearNeo = 0;

            rateClear = getRateClear(currentRate, rateClear);

            if (currentRate > 10000 && currentRate < rate * 100)
            {
                canClearNeo = mount * TEN_POWER / (nep5Price * rateClear);

                if (canClearNeo <= 0)
                    throw new InvalidOperationException("The param is exception.");

                if (canClearNeo > lockedNeo)
                    canClearNeo = lockedNeo;

                BigInteger lastRate = (lockedNeo - canClearNeo) * nep5Price / ((hasDrawed - mount) * SIX_POWER);

                if (lastRate > rescueRate)
                    throw new InvalidOperationException("The param is exception.");
            }

            if (currentRate <= 10000)
            {
                if (mount != hasDrawed)
                    throw new InvalidOperationException("The param is exception.");
                canClearNeo = lockedNeo;
            }

            {
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[2];
                arg[0] = addr;
                arg[1] = mount;
                if (!(bool)SDUSDContract("destory", arg))
                    throw new InvalidOperationException("The destory is exception.");
            }

            sarInfo.locked = lockedNeo - canClearNeo;
            sarInfo.hasDrawed = hasDrawed - mount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            SARInfo ownerSarInfo = Helper.Deserialize(ownerBytes) as SARInfo;
            BigInteger ownerLock = ownerSarInfo.locked;
            ownerSarInfo.locked = ownerLock + canClearNeo;

            Storage.Put(Storage.CurrentContext, ownerKey, Helper.Serialize(ownerSarInfo));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            //notify
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_RESUCE, mount);
            return true;
        }

        /***
         *  getRateClear-->liquidate_dis_rate_c
         *  @param currentRate  11000
         *  @param rateClear    90
         **/
        private static BigInteger getRateClear(BigInteger currentRate, BigInteger rateClear)
        {
            BigInteger ret = rateClear;
            if (currentRate > 0 && rateClear > 0) {
                BigInteger result = 100000000 / currentRate;
                if (result > rateClear * 100) {
                    ret = (result + 100) / 100; 
                }
            }
            return ret;

        }

        private static Boolean rescueT(byte[] otherAddr, byte[] addr, BigInteger bondMount)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (otherAddr.Length != 20)
                throw new InvalidOperationException("The parameter otherAddr SHOULD be 20-byte addresses.");

            if (otherAddr.AsBigInteger() != addr.AsBigInteger())
                throw new InvalidOperationException("The parameter is exception.");

            if (bondMount <= 0)
                throw new InvalidOperationException("The parameter mount MUST be greater than 0.");

            //check state
            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST not be pause.");

            BigInteger state = Storage.Get(Storage.CurrentContext, getBondKey(addr)).AsBigInteger();
            if (state != 1)
                throw new InvalidOperationException("The state is exception.");

            byte[] key = getSARKey(otherAddr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0)
                throw new InvalidOperationException("The sar can not be null.");

            SARInfo sarInfo = (SARInfo)Helper.Deserialize(sar);
            BigInteger lockedNeo = sarInfo.locked;
            BigInteger hasDrawed = sarInfo.hasDrawed;
            BigInteger bondLocked = sarInfo.bondLocked;
            BigInteger bondDrawed = sarInfo.bondDrawed;
            string assetType = sarInfo.assetType;

            byte[] nep5AssetID = Storage.Get(Storage.CurrentContext, getAccountKey(assetType.AsByteArray()));
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] sdusdAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDUSD_ACCOUNT.AsByteArray()));

            if (hasDrawed <= 0)
                throw new InvalidOperationException("The param is exception.");

            if (bondMount > hasDrawed)
                throw new InvalidOperationException("The param is exception.");

            BigInteger nep5Price = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = assetType;
                nep5Price = (BigInteger)OracleContract("getTypeB", arg);
            }

            BigInteger rate = 0;
            BigInteger rescueRate = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = assetType;
                Config config = (Config)OracleContract("getStructConfig", arg);
                rate = config.liquidate_line_rate_c;
                rescueRate = config.liquidate_top_rate_c;
            }

            BigInteger currentRate = lockedNeo * nep5Price / (hasDrawed * 10000);
            if (currentRate >= rate * 100)
                throw new InvalidOperationException("The param is exception.");

            if (currentRate <= 10000)
            {
                if (bondMount != hasDrawed)
                    throw new InvalidOperationException("The param is exception.");
            }

            BigInteger canClearNeo = bondMount * EIGHT_POWER / nep5Price;

            if (currentRate > 10000 && currentRate < rate * 100)
            {
                BigInteger lastRate = (lockedNeo - canClearNeo) * nep5Price / ((hasDrawed - bondDrawed) * EIGHT_POWER);

                if (lastRate > rescueRate)
                    throw new InvalidOperationException("The param is exception.");
            }

            if (canClearNeo >= lockedNeo)
            {
                sarInfo.locked = 0;
                canClearNeo = lockedNeo;
            }
            else
            {
                sarInfo.locked = lockedNeo - canClearNeo;
            }
            sarInfo.hasDrawed = hasDrawed - bondMount;
            sarInfo.bondLocked = bondLocked + canClearNeo;
            sarInfo.bondDrawed = bondDrawed + bondMount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            byte[] bondKey = getBondGlobalKey(BOND_ISSUED_GLOBAL.AsByteArray());
            BigInteger total = Storage.Get(Storage.CurrentContext, bondKey).AsBigInteger();
            Storage.Put(Storage.CurrentContext, bondKey, total + bondMount);

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_BOND_RESUCE, bondMount);
            return true;
        }

        private static BigInteger getConfig(string configKey)
        {
            byte[] key = getConfigKey(configKey.AsByteArray());
            return Storage.Get(Storage.CurrentContext, key).AsBigInteger();
        }

        private static Boolean setConfig(string configKey, BigInteger value)
        {
            byte[] key = getConfigKey(configKey.AsByteArray());
            Storage.Put(Storage.CurrentContext, key, value);
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

            if (mount > hasDrawed)
                throw new InvalidOperationException("The param is exception.");

            byte[] sdusdAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDUSD_ACCOUNT.AsByteArray()));
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] sdsAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));
            byte[] to = ExecutionEngine.ExecutingScriptHash;

            BigInteger sdsPrice = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = CONFIG_PRICE_SDS;
                sdsPrice = (BigInteger)OracleContract("getTypeB", arg);
            }

            BigInteger fee_rate = 148;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = CONFIG_FEE_C;
                fee_rate = (BigInteger)OracleContract("getTypeA", arg);
            }

            uint blockHeight = Blockchain.GetHeight();
            BigInteger fee = sarInfo.fee;
            BigInteger sdsFee = sarInfo.sdsFee;

            uint lastHeight = sarInfo.lastHeight;
            BigInteger currFee = (blockHeight - lastHeight) * hasDrawed * fee_rate / SIXTEEN_POWER;

            BigInteger needUSDFee = (currFee + fee) * mount / hasDrawed;
            BigInteger needFee = needUSDFee * EIGHT_POWER / sdsPrice;

            if (needFee > sdsFee)
                throw new InvalidOperationException("The param is exception.");

            {
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[2];
                arg[0] = addr;
                arg[1] = mount;
                if (!(bool)SDUSDContract("destory", arg)) return false;
            }

            sarInfo.lastHeight = blockHeight;
            sarInfo.fee = currFee + fee - needUSDFee;
            sarInfo.sdsFee = sdsFee - needFee;
            sarInfo.hasDrawed = hasDrawed - mount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = sarInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_WIPE;
            detail.operated = mount;
            detail.hasLocked = locked;
            detail.hasDrawed = sarInfo.hasDrawed;

            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //notify
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_WIPE, mount);

            FeeOperated(addr, sarInfo.txid, txid, needUSDFee, needFee);
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

            BigInteger nep5Price = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = assetType;
                nep5Price = (BigInteger)OracleContract("getTypeB", arg);
            }

            BigInteger rate = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = CONFIG_RATE_C;
                rate = (BigInteger)OracleContract("getTypeA", arg);
            }

            BigInteger hasDrawPNeo = hasDrawed * rate * SIX_POWER / nep5Price;

            if (mount > locked - hasDrawPNeo)
                throw new InvalidOperationException("The param is exception.");

            byte[] from = ExecutionEngine.ExecutingScriptHash;
            if (from.Length == 0)
                throw new InvalidOperationException("The param is exception.");
            {
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = addr;
                arg[2] = mount;
                var nep5Contract = (NEP5Contract)nep5AssetID.ToDelegate();

                if (!(bool)nep5Contract("transfer", arg)) throw new InvalidOperationException("The operation is error.");
            }

            sarInfo.locked = locked - mount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = sarInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_FREE;
            detail.operated = mount;
            detail.hasLocked = locked - mount;
            detail.hasDrawed = hasDrawed;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //notify
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_FREE, mount);
            return true;
        }

        private static Boolean withdrawT(byte[] addr, BigInteger mount)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (mount <= 0)
                throw new InvalidOperationException("The parameter mount MUST be greater than 0.");

            //check SAR
            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST not be pause.");

            byte[] key = getSARKey(addr);

            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0)
                throw new InvalidOperationException("The param is exception.");
            SARInfo sarInfo = (SARInfo)Helper.Deserialize(sar);

            BigInteger locked = sarInfo.locked;
            BigInteger bondLocked = sarInfo.bondLocked;
            BigInteger bondDrawed = sarInfo.bondDrawed;
            BigInteger hasDrawed = sarInfo.hasDrawed;
            string assetType = sarInfo.assetType;

            if (bondDrawed <= 0)
                throw new InvalidOperationException("The param is exception.");

            if (bondLocked <= 0)
                throw new InvalidOperationException("The param is exception.");

            if (mount > bondDrawed)
                throw new InvalidOperationException("The param is exception.");

            byte[] nep5AssetID = Storage.Get(Storage.CurrentContext, getAccountKey(assetType.AsByteArray()));
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] sdusdAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDUSD_ACCOUNT.AsByteArray()));
            byte[] from = ExecutionEngine.ExecutingScriptHash;

            {
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[2];
                arg[0] = addr;
                arg[1] = mount;
                if (!(bool)SDUSDContract("destory", arg))
                    throw new InvalidOperationException("The destory is exception.");
            }

            BigInteger canNeo = bondLocked * mount / bondDrawed;

            sarInfo.locked = locked + canNeo;
            sarInfo.bondLocked = bondLocked - canNeo;
            sarInfo.bondDrawed = bondDrawed - mount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            byte[] bondKey = getBondGlobalKey(BOND_ISSUED_GLOBAL.AsByteArray());
            BigInteger total = Storage.Get(Storage.CurrentContext, bondKey).AsBigInteger();
            Storage.Put(Storage.CurrentContext, bondKey, total - mount);

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = sarInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_BOND_WITHDRAW;
            detail.operated = mount;
            detail.hasLocked = sarInfo.locked;
            detail.hasDrawed = sarInfo.hasDrawed;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //notify
            Operated(addr, sarInfo.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_BOND_WITHDRAW, mount);
            return true;
        }

        private static Boolean openSAR4C(byte[] addr, string assetType)
        {
            var key = getSARKey(addr);
            byte[] bytes = getSAR4C(addr);
            if (bytes.Length > 0) throw new InvalidOperationException("The sar can  be null.");

            //check legal collateral
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            BigInteger assetPrice = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                object[] arg = new object[1];
                arg[0] = assetType;
                assetPrice = (BigInteger)OracleContract("getTypeB", arg);
            }
            if (assetPrice <= 0) return false;

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

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

            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_OPEN;
            detail.operated = 0;
            detail.hasLocked = 0;
            detail.hasDrawed = 0;

            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //notify
            Operated(addr, txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_OPEN, 0);
            return true;
        }

        private static Boolean reserve(byte[] addr, BigInteger lockMount)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (lockMount <= 0)
                throw new InvalidOperationException("The parameter lockMount MUST be greater than 0.");

            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST not be pause.");

            var key = getSARKey(addr);
            byte[] bytes = getSAR4C(addr);
            if (bytes.Length == 0) throw new InvalidOperationException("The sar can  be null.");

            SARInfo sarInfo = Helper.Deserialize(bytes) as SARInfo;

            string assetType = sarInfo.assetType;

            byte[] nep5AssetID = Storage.Get(Storage.CurrentContext, getAccountKey(assetType.AsByteArray()));

            //current contract
            byte[] to = ExecutionEngine.ExecutingScriptHash;
            if (to.Length == 0)
                throw new InvalidOperationException("The parameter to SHOULD be greater than 0.");

            object[] arg = new object[3];
            arg[0] = addr;
            arg[1] = to;
            arg[2] = lockMount;

            var AssetContract = (NEP5Contract)nep5AssetID.ToDelegate();

            if (!(bool)AssetContract("transfer", arg))
                throw new InvalidOperationException("The operation is exception.");

            sarInfo.locked = sarInfo.locked + lockMount;
            BigInteger currLock = sarInfo.locked;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = sarInfo.txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_LOCK;
            detail.operated = lockMount;
            detail.hasLocked = currLock;
            detail.hasDrawed = sarInfo.hasDrawed;
            detail.txid = txid;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //notify
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

            byte[] sdsAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));

            //current account
            //byte[] to = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));
            byte[] to = ExecutionEngine.ExecutingScriptHash;

            object[] arg = new object[3];
            arg[0] = addr;
            arg[1] = to;
            arg[2] = mount;

            var AssetContract = (NEP5Contract)sdsAssetID.ToDelegate();

            if (!(bool)AssetContract("transfer", arg))
                throw new InvalidOperationException("The operation is exception.");

            BigInteger sdsFee = sarInfo.sdsFee;
            sarInfo.sdsFee = sdsFee + mount;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

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

            BigInteger allSd = locked * assetPrice / (rate * SIX_POWER);

            if (allSd < hasDrawed + drawMount)
                throw new InvalidOperationException("The sar can draw larger than max.");

            BigInteger totalSupply = 0;
            {
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[0];
                totalSupply = (BigInteger)SDUSDContract("totalSupply", arg);
            }

            if (totalSupply + drawMount > releaseMax)
                throw new InvalidOperationException("The sar can draw larger than releaseMax.");

            {
                var SDUSDContract = (NEP5Contract)sdusdAssetID.ToDelegate();
                object[] arg = new object[2];
                arg[0] = addr;
                arg[1] = drawMount;
                if (!(bool)SDUSDContract("increase", arg))
                    throw new InvalidOperationException("The sdusd increased error.");
            }

            uint blockHeight = Blockchain.GetHeight();
            BigInteger fee = sarInfo.fee;

            if (hasDrawed == 0)
            {
                sarInfo.lastHeight = blockHeight;
                sarInfo.fee = 0;

            }
            else
            {

                uint lastHeight = sarInfo.lastHeight;

                BigInteger currFee = (blockHeight - lastHeight) * hasDrawed * fee_rate / SIXTEEN_POWER;

                sarInfo.lastHeight = blockHeight;
                sarInfo.fee = currFee + fee;
            }
            sarInfo.hasDrawed = hasDrawed + drawMount;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sarInfo));


            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = sarInfo.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_DRAW;
            detail.operated = drawMount;
            detail.hasLocked = locked;
            detail.hasDrawed = sarInfo.hasDrawed;
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
            BigInteger fee = sarInfo.fee;
            BigInteger sdsFee = sarInfo.sdsFee;
            BigInteger bondDrawed = sarInfo.bondDrawed;
            string assetType = sarInfo.assetType;

            if (locked > 0) throw new InvalidOperationException("The locked count must be 0.");

            if (hasDrawed > 0) throw new InvalidOperationException("The expand count must be 0.");

            if (fee > 0) throw new InvalidOperationException("The param is exception.");

            if (sdsFee > 0) throw new InvalidOperationException("The param is exception.");

            if (bondDrawed > 0) throw new InvalidOperationException("The param is exception.");

            Storage.Delete(Storage.CurrentContext, key);

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

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

            //creator
            public byte[] owner;

            //key of this SAR
            public byte[] txid;

            //amount of locked collateral
            public BigInteger locked;

            //amount of issued sdusd  
            public BigInteger hasDrawed;

            //type of collateral 
            public string assetType;

            //1safe  2unsafe 3lock   
            public int status;

            //amount of used bond
            public BigInteger bondLocked;

            //amount of sdusd liquidated by bond
            public BigInteger bondDrawed;

            //block 
            public uint lastHeight;

            //amount of stable fee(sdusd)
            public BigInteger fee;

            //amount of locked sds
            public BigInteger sdsFee;

        }

        public class BondSAR
        {
            //creator
            public byte[] owner;

            //key of this SAR
            public byte[] txid;


        }

        public class SARTransferDetail
        {
            public byte[] from;

            public byte[] sarTxid;

            public byte[] txid;

            public BigInteger operated;

            public BigInteger hasLocked;

            public BigInteger hasDrawed;

            public int type;
        }

        public class Config
        {
            public BigInteger liquidate_line_rate_b;

            public BigInteger liquidate_line_rate_c;

            public BigInteger liquidate_dis_rate_c;

            public BigInteger fee_rate_c;

            public BigInteger liquidate_top_rate_c;

            public BigInteger liquidate_line_rateT_c;

            public BigInteger issuing_fee_c;

            public BigInteger issuing_fee_b;

            public BigInteger debt_top_c;

        }
    }
}
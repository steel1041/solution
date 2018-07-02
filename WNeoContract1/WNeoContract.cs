using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework.Services.System;

namespace WNeoContract1
{
    public class WNeoContract : SmartContract
    {
        //NEO Asset
        private static readonly byte[] neo_asset_id = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };

        //gas 0x602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7
        //反序  e72d286979ee6cb1b7e65dfddfb2e384100b8d148e7758de42e4168b71792c60
        private static readonly byte[] gas_asset_id = Helper.HexToBytes("e72d286979ee6cb1b7e65dfddfb2e384100b8d148e7758de42e4168b71792c60");

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        [DisplayName("approve")]
        public static event Action<byte[], byte[], BigInteger> Approved;

        [Appcall("95e6b39d3557f5ba5ba59fab178f6de3c24e3d04")] //JumpCenter ScriptHash
        public static extern object JumpCenterContract(string method, object[] args);

        //配置参数-NEO市场价格
        private const string CONFIG_PRICE_NEO = "neo_price";

        //配置参数-GAS市场价格
        private const string CONFIG_PRICE_GAS = "gas_price";

        //超级管理员账户
        private static readonly byte[] SuperAdmin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7"); 

        //因子
        private const ulong factor = 100000000;

        //总计数量
        private const ulong TOTAL_AMOUNT = 0;

        private const string TOTAL_SUPPLY = "totalSupply";

        private const string TOTAL_DESTORY = "totalDestory";


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
            var magicstr = "2018-06-27 15:04:10";

            if (Runtime.Trigger == TriggerType.Verification)//取钱才会涉及这里
            {
                //return Runtime.CheckWitness(SuperAdmin);
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
                if (operation == "mintTokens")
                {
                    if (args.Length != 1) return 0;
                    string type = (string)args[0];
                    return mintTokens(type);
                }

                //W兑换P，销毁W
                if (operation == "WNeoToPNeo")
                {
                    if (args.Length != 2) return false;

                    byte[] addr = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];
                    //判断调用者是否是跳板合约
                    byte[] jumpCallScript = Storage.Get(Storage.CurrentContext, "callScript");
                    if (callscript.AsBigInteger() != jumpCallScript.AsBigInteger()) return false;

                    return destoryByP(addr, null, value);
                }

                //PNeo兑换WNeo
                if (operation == "PNeoToWNeo")
                {
                    if (args.Length != 2) return false;

                    byte[] addr = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
                    object[] param = new object[3];
                    param[0] = addr;
                    param[1] = txid;
                    param[2] = value;

                    //通过跳板合约调用P
                    if (!(bool)JumpCenterContract(operation, param)) return false;
                    return increase(addr, value);
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
            return "W NEO";
        }
        public static string symbol()
        {
            return "WNEO";
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



        private static BigInteger totalDestory()
        {
            return Storage.Get(Storage.CurrentContext,TOTAL_DESTORY).AsBigInteger();
        }

        private static bool setCallScript(byte[] callScript)
        {
            Storage.Put(Storage.CurrentContext, "callScript", callScript);
            return true;
        }

        private static byte[] getJumpCallScript()
        {
            return Storage.Get(Storage.CurrentContext, "callScript");
        }

        private static BigInteger currentMountByW(byte[] txid)
        {
            return Storage.Get(Storage.CurrentContext, txid).AsBigInteger();
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
            byte[] v = Storage.Get(Storage.CurrentContext, txid);
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
            Storage.Put(Storage.CurrentContext, txid, txinfo);
        }

        public static bool mintTokens(string type)
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
            BigInteger realValue = getRealValue(type,value);

            //改变总量
            operateTotalSupply(realValue);

            operateTotalDestory(who,realValue);
            return transfer(null, who, realValue);
        }

        private static BigInteger getRealValue(string type, ulong value)
        {
            if (value <= 0) return 0;
            if (type == "gas") {
                BigInteger neoPrice = getConfig(CONFIG_PRICE_NEO);
                BigInteger gasPrice = getConfig(CONFIG_PRICE_GAS);
                if (neoPrice == 0 || gasPrice == 0) {
                    return value * 2/10;
                }
                return value * gasPrice/neoPrice;
            }
            return value;
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

        //增发货币
        public static bool increase(byte[] to, BigInteger value)
        {
            if (value <= 0) return false;
            if (!Runtime.CheckWitness(to)) return false;

            transfer(null, to, value);

            operateTotalSupply(value);
            return true;
        }

        //销毁货币
        public static bool destoryByP(byte[] from,byte[] txid,BigInteger value)
        {
            if (value <= 0) return false;
            if (!Runtime.CheckWitness(from)) return false;

            transfer(from, null, value);

            BigInteger current = Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
            if (current - value >= 0)
            {
                Storage.Put(Storage.CurrentContext, TOTAL_SUPPLY, current - value);
            }

            //operateTotalSupply(0 - value);
            ////记录总抵押量
            //operateTotalDestory(from,value);
            return true;
        }

        private static bool operateTotalDestory(byte[] from, BigInteger value)
        {
            BigInteger curr = Storage.Get(Storage.CurrentContext,TOTAL_DESTORY).AsBigInteger();
            Storage.Put(Storage.CurrentContext,TOTAL_DESTORY,curr + value);
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
            if (!Runtime.CheckWitness(SuperAdmin)) return false;
            Storage.Put(Storage.CurrentContext, key.AsByteArray(), value);
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

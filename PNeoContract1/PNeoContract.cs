﻿using Neo.SmartContract.Framework;
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
         
        [Appcall("c434d9c2241f9e6bc73f728cf0774b37d4299f3e")] //JumpCenter ScriptHash
        public static extern object JumpCenterContract(string method, object[] args);

        //[Appcall("60be83c7ef0742450c3530b3de9abc33a9d1050f")] //WNEOContract ScriptHash
        //public static extern object WNEOContract(string method, object[] args);

        //超级管理员账户
        private static readonly byte[] SuperAdmin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        //static readonly byte[] jumpContract = Helper.ToScriptHash("AJTCnzAkMETzxLDmhNgxdJkUUJzXpT1Jhy");    //PNeoJumpContract address
        //private static readonly byte[] SuperAdmin = Helper.ToScriptHash("Aeto8Loxsh7nXWoVS4FzBkUrFuiCB3Qidn");

        //nep5 func
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
        }
        public static string Name()
        {
            return "P NEO";
        }
        public static string Symbol()
        {
            return "PNEO";
        }
        //因子
        private const ulong factor = 100000000;

        //总计数量
        private const ulong TOTAL_AMOUNT = 0;

        private const string TOTAL_SUPPLY = "totalSupply";

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
            //必须在入口函数取得callscript，调用脚本的函数，也会导致执行栈变化，再取callscript就晚了  
            var callscript = ExecutionEngine.CallingScriptHash;

            var magicstr = "2018-05-23 14:38:10";

            if (Runtime.Trigger == TriggerType.Verification)//取钱才会涉及这里
            {
                return Runtime.CheckWitness(SuperAdmin);
            }

            else if (Runtime.Trigger == TriggerType.Application)
            {
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

                //WNeo换成PNeo(动态调用WNeo)
                if (operation == "WNeoToPNeo")
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

                    //Storage.Put(Storage.CurrentContext, txid, value);
                    if (!(bool)JumpCenterContract(operation, param)) return false;
                    return IncreaseBySelf(addr, value);
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

                    return DestoryByW(addr,txid,value);
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

                    return DestoryBySD(addr,txid,value);
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
                    byte[] jumpCallScript = getJumpCallScript();
                    if (callscript.AsBigInteger() != jumpCallScript.AsBigInteger()) return false;
                    return IncreaseBySD(addr,txid,value);
                }
                //查询当前存的金额数量
                if (operation == "currentMountByP")
                {
                    if (args.Length != 1) return false;
                    byte[] txid = (byte[])args[0];
                    return currentMountByP(txid);
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


            }
            return false;
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

        public static TransferInfo GetTXInfo(byte[] txid)
        {
            byte[] v = Storage.Get(Storage.CurrentContext, txid);
            if (v.Length == 0)
                return null;

            //老式实现方法
            //TransferInfo info = new TransferInfo();
            //int seek = 0;
            //var fromlen = (int)v.AsString().Substring(seek, 2).AsByteArray().AsBigInteger();
            //seek += 2;
            //info.from = v.AsString().Substring(seek, fromlen).AsByteArray();
            //seek += fromlen;
            //var tolen = (int)v.AsString().Substring(seek, 2).AsByteArray().AsBigInteger();
            //seek += 2;
            //info.to = v.AsString().Substring(seek, tolen).AsByteArray();
            //seek += tolen;
            //var valuelen = (int)v.AsString().Substring(seek, 2).AsByteArray().AsBigInteger();
            //seek += 2;
            //info.value = v.AsString().Substring(seek, valuelen).AsByteArray().AsBigInteger();
            //return info;

            //新式实现方法只要一行
            return (TransferInfo)Helper.Deserialize(v);
        }

        private static void setTxInfo(byte[] from, byte[] to, BigInteger value)
        {
            //因为testnet 还在2.6，限制

            TransferInfo info = new TransferInfo();
            info.from = from;
            info.to = to;
            info.value = value;

            //用一个老式实现法
            //byte[] txinfo = byteLen(info.from.Length).Concat(info.from);
            //txinfo = txinfo.Concat(byteLen(info.to.Length)).Concat(info.to);
            //byte[] _value = value.AsByteArray();
            //txinfo = txinfo.Concat(byteLen(_value.Length)).Concat(_value);
            //新式实现方法只要一行
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

        //增发货币
        public static bool IncreaseBySD(byte[] to,byte[] txid,BigInteger value)
        {
            if (value <= 0) return false;

            //查询SD合约是否有相应的值
            //object[] param = new object[1];
            //param[0] = txid;

            //var currentMount = (BigInteger)JumpCenterContract("currentMountBySD",param);
            //if (currentMount != value) return false;

            Transfer(null, to, value);

            operateTotalSupply(value);
            return true;
        }
        //增发货币
        public static bool IncreaseBySelf(byte[] to,  BigInteger value)
        {
            if (value <= 0) return false;

            Transfer(null, to, value);

            operateTotalSupply(value);
            return true;
        }

        //销毁货币
        public static bool DestoryBySD(byte[] from, byte[] txid, BigInteger value)
        {
            if (value <= 0) return false;

            //object[] param = new object[1];
            //param[0] = txid;
            ////查询SD合约
            //var currentMount = (BigInteger)JumpCenterContract("currentMountBySD", param);
            //if (currentMount != value) return false;

            Transfer(from, null, value);

            operateTotalSupply(0-value);
            return true;
        }

        //销毁货币
        public static bool DestoryByW(byte[] from, byte[] txid,BigInteger value)
        {
            if (value <= 0) return false;
            if (!Runtime.CheckWitness(from)) return false;

            object[] param = new object[1];
            param[0] = txid;
            //查询W合约
            var currentMount = (BigInteger)JumpCenterContract("currentMountByW", param);
            if (currentMount != value) return false;

            Transfer(from, null, value);

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

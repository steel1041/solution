using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework.Services.System;

namespace SDTContract1
{
    public class SDTContract : SmartContract
    {
        //NEO Asset
        private static readonly byte[] neo_asset_id = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        [DisplayName("refund")]
        public static event Action<byte[], byte[], BigInteger> Refunded;

        [DisplayName("approve")]
        public static event Action<byte[], byte[], BigInteger> Approved;

        //超级管理员账户
        //testnet账户  AaBmSJ4Beeg2AeKczpXk89DnmVrPn3SHkU
        private static readonly byte[] SuperAdmin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        //nep5 func
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
        }
        public static string Name()
        {
            return "Special Drawing Token";
        }
        public static string Symbol()
        {
            return "SDT";
        }
        //因子
        private const ulong factor = 100000000;

        //总计数量
        private const ulong TOTAL_AMOUNT = 1000000000 * factor;

        private const string BLACK_HOLE_ACCOUNT_01 = "blackHoleAccount01";

        private const string BLACK_HOLE_ACCOUNT_02 = "blackHoleAccount02";

        private const string BLACK_HOLE_TYPE = "blackHoleType";

        private const string MINT_TYPE = "mintType";

        private const string CALL_SCRIPT = "callScript";

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
            var magicstr = "2018-05-28 16:30:10";

            if (Runtime.Trigger == TriggerType.Verification)//取钱才会涉及这里
            {
                if (SuperAdmin.Length == 20)
                {
                    return Runtime.CheckWitness(SuperAdmin);
                }
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
                if (operation == "init")
                {
                    return Init();
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

                    //检测转出账户是否是黑洞账户,是就返回false
                    byte[] blackHoleAccount = getBlackHoleScript(); 
                    if (blackHoleAccount.Length > 0 && from.AsBigInteger() == blackHoleAccount.AsBigInteger()) return false; 

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
                if (operation == "cancelApprove")
                {
                    //args[0]发起人账户  args[1]被授权账户
                    return CancelApprove((byte[])args[0], (byte[])args[1]);
                }
                if (operation == "transferFrom")
                {
                    //args[0]转账账户  args[1]被授权账户 args[2]被转账账户   args[3]被转账金额
                    return TransferFrom((byte[])args[0], (byte[])args[1], (byte[])args[2], (BigInteger)args[3]);
                }
                if (operation == "getTXInfo")
                {
                    if (args.Length != 1) return 0;
                    byte[] txid = (byte[])args[0];
                    return GetTXInfo(txid);
                }
                if (operation == "setBlackHole")
                {//设置黑洞账户，只有超级管理员才有权限

                    if (args.Length != 1) return false;

                    byte[] account = (byte[])args[0];

                    return SetBlackHoleAccount(account);
                }
                if (operation == "burn")
                {//销毁
                    if (args.Length != 2) return false;

                    byte[] from = (byte[])args[0];

                    BigInteger value = (BigInteger)args[1];
                    
                    byte[] blackHoleAccount = getBlackHoleScript();

                    if (from.AsBigInteger() != blackHoleAccount.AsBigInteger()) return false; //判断是否是黑洞账户,且只有黑洞账户才能执行销毁功能.

                    return Burn(from, value);
                }
                //按照业务规则，在前提条件下增发
                if (operation == "mint")
                {
                    BigInteger type = getMintType(MINT_TYPE);
                    byte[] addr = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];
                    //合约调用
                    if (type == 1){
                        //判断调用者是否是跳板合约
                        byte[] jumpCallScript = getJumpCallScript();
                        if (callscript.AsBigInteger() != jumpCallScript.AsBigInteger()) return false;

                        if (!Runtime.CheckWitness(addr)) return false;
                        return mintToken(addr,value);
                    }
                    //管理员设置
                    else if (type == 2){
                        if (!Runtime.CheckWitness(SuperAdmin)) return false;
                        return mintToken(SuperAdmin,value);
                    }
                }
                //设置合约参数
                if (operation == "setCallScript")
                {
                    if (args.Length != 2) return false;
                    string type = (string)args[0];

                    //超级管理员设置跳板合约地址
                    if (!Runtime.CheckWitness(SuperAdmin)) return false;

                    if (type == CALL_SCRIPT) {
                        byte[] addr = (byte[])args[1];
                        return setCallScript(type, addr);
                    }
                    if (type == BLACK_HOLE_TYPE || type == MINT_TYPE) {
                        BigInteger holeType = (BigInteger)args[1];
                        return setHoleType(type, holeType);
                    }
                    return false;
                }
                
            }
            return false;
        }

        private static bool mintToken(byte[] addr, BigInteger value)
        {
            Transfer(null,addr,value);
            BigInteger current = Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
            if (current + value >= 0)
            {
                Storage.Put(Storage.CurrentContext, TOTAL_SUPPLY, current + value);
            }
            return true;
        }

        private static byte[] getJumpCallScript()
        {
            return Storage.Get(Storage.CurrentContext, CALL_SCRIPT);
        }

        private static BigInteger getMintType(string v)
        {
            return Storage.Get(Storage.CurrentContext,v).AsBigInteger();
        }

        private static bool setCallScript(string type,byte[] callScript)
        {
            //BLACK_HOLE_TYPE、CALL_SCRIPT
            Storage.Put(Storage.CurrentContext, type, callScript);
            return true;
        }

        private static bool setHoleType(string type, BigInteger callScript)
        {
            //BLACK_HOLE_TYPE、CALL_SCRIPT
            Storage.Put(Storage.CurrentContext, type, callScript);
            return true;
        }

        private static byte[] getBlackHoleScript()
        {
            //允许设置多个账户，限制2个
            byte[] account1 = Storage.Get(Storage.CurrentContext, BLACK_HOLE_ACCOUNT_01);
            byte[] account2 = Storage.Get(Storage.CurrentContext, BLACK_HOLE_ACCOUNT_02);
            BigInteger blackHoleType = Storage.Get(Storage.CurrentContext, BLACK_HOLE_TYPE).AsBigInteger();

            if (account1.Length > 0 && blackHoleType == 1) return account1;
            if (account2.Length > 0 && blackHoleType == 2) return account2;

            return account1;
        }

        public static TransferInfo GetTXInfo(byte[] txid)
        {
            byte[] v = Storage.Get(Storage.CurrentContext, txid);
            if (v.Length == 0)
                return null;


            //老式实现方法
            TransferInfo info = new TransferInfo();
            int seek = 0;
            var fromlen = (int)v.Range(seek, 2).AsBigInteger();
            seek += 2;
            info.from = v.Range(seek, fromlen);
            seek += fromlen;
            var tolen = (int)v.Range(seek, 2).AsBigInteger();
            seek += 2;
            info.to = v.Range(seek, tolen);
            seek += tolen;
            var valuelen = (int)v.Range(seek, 2).AsBigInteger();
            seek += 2;
            info.value = v.Range(seek, valuelen).AsBigInteger();
            return info;

            //新式实现方法只要一行
            //return (TransferInfo)Helper.Deserialize(v);
        }


        public static bool SetBlackHoleAccount(byte[] account)
        {//设置管理费账户,只有超管才有权限
            if (!Runtime.CheckWitness(SuperAdmin)) return false;
            //允许设置多个账户，限制2个
            byte[] account1 = Storage.Get(Storage.CurrentContext, BLACK_HOLE_ACCOUNT_01);    
            byte[] account2 = Storage.Get(Storage.CurrentContext, BLACK_HOLE_ACCOUNT_02);

            int a1 = account1.Length;
            int a2 = account2.Length;
            if (a1 > 0 && a2 > 0) return false;

            //账户不能重复设置
            if (a1 > 0 && (account1.AsBigInteger() == account.AsBigInteger())) return false;

            if (a2 > 0 && (account2.AsBigInteger() == account.AsBigInteger())) return false;

            //两个账户哪个没有就设置哪个，如果都有就不能设置
            if (a1 == 0) {
                Storage.Put(Storage.CurrentContext,BLACK_HOLE_ACCOUNT_01,account);
            }
            if (a2 == 0) {
                Storage.Put(Storage.CurrentContext, BLACK_HOLE_ACCOUNT_02,account);
            }
            return true;
        }
         
        private static void setTxInfo(byte[] from, byte[] to, BigInteger value)
        {
            //因为testnet 还在2.6，限制

            TransferInfo info = new TransferInfo();
            info.from = from;
            info.to = to;
            info.value = value;

            //用一个老式实现法

            //优化的拼包方法
            var data = info.from;
            var lendata = ((BigInteger)data.Length).AsByteArray().Concat(doublezero).Range(0, 2);
            ////lendata是数据长度得bytearray，因为bigint长度不固定，统一加两个零，然后只取前面两个字节
            ////为什么要两个字节，因为bigint是含有符号位得，统一加个零安全，要不然长度129取一个字节就是负数了
            var txinfo = lendata.Concat(data);

            data = info.to;
            lendata = ((BigInteger)data.Length).AsByteArray().Concat(doublezero).Range(0, 2);
            txinfo = txinfo.Concat(lendata).Concat(data);

            data = value.AsByteArray();
            lendata = ((BigInteger)data.Length).AsByteArray().Concat(doublezero).Range(0, 2);
            txinfo = txinfo.Concat(lendata).Concat(data);
            //新式实现方法只要一行
            //byte[] txinfo = Helper.Serialize(info);

            var txid = (ExecutionEngine.ScriptContainer as Transaction).Hash;
            Storage.Put(Storage.CurrentContext, txid, txinfo);
        }

        //把doublezero定义出来就好了，...... 要查编译器了
        static readonly byte[] doublezero = new byte[2] { 0x00, 0x00 };
        
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
        ///   Cancel another account to transfer amount tokens from the owner acount
        /// </summary>
        /// <param name="owner">
        ///   The account to invoke cancel approve.
        /// </param>
        /// <param name="spender">
        ///   The account to grant TransferFrom access to.
        /// </param>
        /// </param>
        /// <returns>
        ///   Transaction Successful?
        /// </returns>
        public static bool CancelApprove(byte[] owner, byte[] spender)
        {
            if (owner.Length != 20 || spender.Length != 20) return false;
            if (!Runtime.CheckWitness(owner)) return false;
            if (owner == spender) return true;
            Storage.Delete(Storage.CurrentContext, owner.Concat(spender));
            Approved(owner, spender, 0);
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

        //销毁货币
        public static bool Burn(byte[] from, BigInteger value)
        { 
            if (value <= 0) return false;

            if (!Runtime.CheckWitness(from)) return false;

            Transfer(from, null, value);

            operateTotalSupply(0 - value);
            return true;
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

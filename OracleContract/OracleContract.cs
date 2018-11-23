﻿using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.Numerics;
using Neo.SmartContract.Framework.Services.System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections;

namespace OracleContract
{
    public class Contract1 : SmartContract
    { 

        //事件类型
        private static readonly int EVENT_TYPE_SET_TYPEA = 1;
        private static readonly int EVENT_TYPE_SET_TYPEB = 2;
        private static readonly int EVENT_TYPE_SET_ACCOUNT = 3;
        private static readonly int EVENT_TYPE_SET_ADDR = 4;
        private static readonly int EVENT_TYPE_SET_MEDIAN = 5; 
        private static readonly int EVENT_TYPE_REMOVE_ADDR = 6;
          

        //管理员账户  
        [DisplayName("oracleOperator")]
        public static event Action<byte[], byte[], byte[], BigInteger, int> Operated;
         
        //admin账户
        private const string ADMIN_ACCOUNT = "admin_account"; 
        private static readonly byte[] admin = Helper.ToScriptHash("AQdP56hHfo54JCWfpPw4MXviJDtQJMtXFa");

        private static byte[] GetTypeAParaKey(byte[] account) => new byte[] { 0x01 }.Concat(account);
        private static byte[] GetTypeAKey(string strKey) => new byte[] { 0x02 }.Concat(strKey.AsByteArray());
        private static byte[] GetTypeBKey(string key) => new byte[] { 0x03 }.Concat(key.AsByteArray());

        private static byte[] GetParaAddrKey(string paraKey, byte[] addr) => new byte[] { 0x10 }.Concat(paraKey.AsByteArray().Concat(addr));
        //private static byte[] GetParaCountKey(string paraKey) => new byte[] { 0x11 }.Concat(paraKey.AsByteArray());
        //private static byte[] GetAddrIndexKey(string paraKey, byte[] addr) => new byte[] { 0x13 }.Concat(paraKey.AsByteArray().Concat(addr));

        private static byte[] GetMedianKey(string key) => new byte[] { 0x20 }.Concat(key.AsByteArray());
        private static byte[] GetAverageKey(string key) => new byte[] { 0x21 }.Concat(key.AsByteArray());
         
        private static byte[] GetConfigKey(byte[] key) => new byte[] { 0x30 }.Concat(key);

        private static byte[] GetAccountKey(string key) => new byte[] { 0x40 }.Concat(key.AsByteArray());
         
        public static Object Main(string operation, params object[] args)
        {
            var callscript = ExecutionEngine.CallingScriptHash;

            var magicstr = "2018-10-24 14:10";
         
            /*设置全局参数
            * liquidate_rate_b 150
            * warning_rate_c 120
            */
            /*设置锚定物白名单
             *anchor_type_gold   0:黑名单 非0:白名单
             */
            if (operation == "setTypeA")
            {
                if (args.Length != 2) return false;

                string key = (string)args[0];

                BigInteger value = (BigInteger)args[1];

                return setTypeA(key, value);
            }

            if (operation == "getTypeA")
            {
                if (args.Length != 1) return false;

                string key = (string)args[0];

                return getTypeA(key);
            }

            if (operation == "setAccount")
            {
                if (args.Length != 2) return false;
                //操作地址，验证当前管理员账户
                string key = (string)args[0];
                byte[] address = (byte[])args[1];

                if (!checkAdmin()) return false;

                return setAccount(key, address);
            }

            if (operation == "getAccount")
            {
                if (args.Length != 1) return false;
                string key = (string)args[0];

                return Storage.Get(Storage.CurrentContext, GetAccountKey(key));
            }

            //管理员添加某个参数合法外部喂价器地址
            if (operation == "addParaAddrWhit")
            {
                if (args.Length != 3) return false;

                string para = (string)args[0];

                byte[] addr = (byte[])args[1];

                BigInteger state = (BigInteger)args[2]; //设置授权状态state != 0 授权

                return addParaAddrWhit(para, addr, state);
            }

            //管理员移除某个参数合法外部喂价器地址
            if (operation == "removeParaAddrWhit")
            {
                if (args.Length != 2) return false;

                string para = (string)args[0];

                byte[] addr = (byte[])args[1];

                return removeParaAddrWhit(para, addr);
            }

            //根据Para查询已授权喂价器地址和状态
            if (operation == "getApprovedAddrs")
            {
                if (args.Length != 1) return false;

                string para = (string)args[0];

                byte[] prefix = GetParaAddrKey(para, new byte[]{ });
                  
                return getDataWithPrefix(prefix);
            }

            //根据Para查询喂价器地址和价格
            if (operation == "getAddrWithParas")
            {
                if (args.Length != 1) return false;

                string para = (string)args[0];
                  
                byte[] prefix = new byte[] { };

                return getDataWithPara(para); 
            }

                /* 设置代币价格  
                *  neo_price    50*100000000
                *  gas_price    20*100000000  
                *  sds_price    0.08*100000000 
                */
                 
                //设置锚定物对应100000000美元汇率
                /*  
                 *  anchor_type_usd    1*100000000
                 *  anchor_type_cny    6.8*100000000
                 *  anchor_type_eur    0.875*100000000
                 *  anchor_type_jpy    120*100000000
                 *  anchor_type_gbp    0.7813 *100000000
                 *  anchor_type_gold   0.000838 * 100000000
                 */

                if (operation == "setTypeB")
            {
                if (args.Length != 3) return false;

                string para = (string)args[0];

                byte[] from = (byte[])args[1];

                BigInteger value = (BigInteger)args[2];

                BigInteger state = (BigInteger)Storage.Get(Storage.CurrentContext, GetParaAddrKey(para, from)).AsBigInteger();

                //允许合约或者授权账户调用
                if (callscript.AsBigInteger() != from.AsBigInteger() && state == 0) return false;

                return setTypeB(para, from, value);
            }

            if (operation == "getTypeB")
            {
                if (args.Length != 1) return false; 
                string key = (string)args[0];

                return getTypeB(key);
            }

            if (operation == "getStructConfig")
            {
                return getStructConfig();
            }

            if (operation == "setStructConfig")
            {
                if (!checkAdmin()) return false;
                return setStructConfig();
            }

            #region 升级合约,耗费490,仅限管理员
            if (operation == "upgrade")
            {
                //不是管理员 不能操作
                if (!checkAdmin()) return false;

                if (args.Length != 1 && args.Length != 9)
                    return false;

                byte[] script = Blockchain.GetContract(ExecutionEngine.ExecutingScriptHash).Script;
                byte[] new_script = (byte[])args[0];
                //如果传入的脚本一样 不继续操作
                if (script == new_script)
                    return false;

                byte[] parameter_list = new byte[] { 0x07, 0x10 };
                byte return_type = 0x05;
                //1|0|4
                bool need_storage = (bool)(object)05;
                string name = "datacenter";
                string version = "1";
                string author = "alchemint";
                string email = "0";
                string description = "alchemint";

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

            return true;
        }

        private static bool checkAdmin()
        {
            byte[] currAdmin = Storage.Get(Storage.CurrentContext, GetAccountKey(ADMIN_ACCOUNT));
             
            if (currAdmin.Length > 0)
            {
                //当前地址和配置地址必须一致
                if (!Runtime.CheckWitness(currAdmin)) return false;
            }
            else
            {
                if (!Runtime.CheckWitness(admin)) return false;
            }
            return true;
        }

        public static bool setAccount(string key, byte[] address)
        {
            if (address.Length != 20)
                throw new InvalidOperationException("The parameters address and to SHOULD be 20-byte addresses.");

            Storage.Put(Storage.CurrentContext, GetAccountKey(key), address);

            Operated(address, key.AsByteArray(), null, 0, EVENT_TYPE_SET_ACCOUNT);

            return true;
        }

        public static bool addParaAddrWhit(string para, byte[] addr, BigInteger state)
        {
            if (!checkAdmin()) return false;

            if (addr.Length != 20) return false;

            byte[] byteKey = GetParaAddrKey(para, addr);

            if (Storage.Get(Storage.CurrentContext, byteKey).AsBigInteger() != 0 || state == 0) return false;

            Storage.Put(Storage.CurrentContext, byteKey, state);
            
            Operated(addr, para.AsByteArray(), null, state, EVENT_TYPE_SET_ADDR);

            return true;
        }

        public static bool removeParaAddrWhit(string para, byte[] addr)
        {
            if (!checkAdmin()) return false;

            byte[] paraAddrByteKey = GetParaAddrKey(para, addr);

            Storage.Delete(Storage.CurrentContext, paraAddrByteKey);
             
            Map<byte[],BigInteger> map = getObjectWithKey(para);
            
            map.Remove(addr);

            Storage.Put(Storage.CurrentContext, GetTypeBKey(para), map.Serialize());
            
            Operated(addr, para.AsByteArray(), null, 0, EVENT_TYPE_REMOVE_ADDR);

            return true;
        }

        public static Map<byte[], BigInteger> getObjectWithKey(string key)
        {
            byte[] data = Storage.Get(Storage.CurrentContext, GetTypeBKey(key));

            Map<byte[], BigInteger> map = new Map<byte[], BigInteger>();

            if (data.Length > 0)

                map = data.Deserialize() as Map<byte[], BigInteger>;

            return map;
        }

        public static Object getDataWithPara(string para)
        {
            
            Map<byte[], BigInteger> map = getObjectWithKey(para);
             
            var array = new Object[map.Values.Length];

            int index = 0;

            foreach (byte[] addr in map.Keys) {

                NodeObj obj = new NodeObj();

                obj.value = map[addr];

                obj.addr = addr;

                array[index] = obj;

                index ++;
            }
            

            return array;
        }
         
        public static Object getDataWithPrefix(byte[] prefix)
        { 
            int count = 0;

            Map<byte[], BigInteger> map = new Map<byte[], BigInteger>();

            Iterator<byte[], byte[]> iterator = Storage.Find(Storage.CurrentContext, prefix);

            while (iterator.Next())
            {
                if (iterator.Key.Range(0,prefix.Length) == prefix)
                {
                    count++;

                    byte[] rawKey = iterator.Key;

                    byte[] addr = rawKey.Range(prefix.Length, rawKey.Length - prefix.Length);

                    BigInteger value = iterator.Value.AsBigInteger();

                    map[addr] = value;
                }
            }
             
            var objs = new Object[count];

            if (count == 0) return objs;

            int index = 0;

            foreach (byte[] addr in map.Keys)
            { 
                NodeObj obj = new NodeObj();
                obj.addr = addr;
                obj.value = map[addr];

                objs[index] = obj;

                index++;
            }
             
            return objs;
            
        }

        public static bool setTypeA(string key, BigInteger value)
        {
            if (key == null || key == "") return false;

            if (!checkAdmin()) return false;

            byte[] byteKey = GetTypeAKey(key);

            Storage.Put(Storage.CurrentContext, byteKey, value);

            byte[] currAdmin = Storage.Get(Storage.CurrentContext, GetAccountKey(ADMIN_ACCOUNT));
             
            if (currAdmin.Length == 0) currAdmin = admin;

            Operated(currAdmin, key.AsByteArray(), null, value,EVENT_TYPE_SET_TYPEA);
            return true;
        }

        public static BigInteger getTypeA(string key)
        {
            byte[] byteKey = GetTypeAKey(key);

            BigInteger value = Storage.Get(Storage.CurrentContext, byteKey).AsBigInteger();

            return value;
        }
        

        public static bool setTypeB(string key, byte[] addr, BigInteger value)
        {
            if (key == null || key == "") return false;

            if (value <= 0) return false;

            if (!Runtime.CheckWitness(addr)) return false;
             
            Map<byte[], BigInteger> map = getObjectWithKey(key);
             
            map[addr] = value;

            Storage.Put(Storage.CurrentContext, GetTypeBKey(key), map.Serialize());
              
            return true;
        }

        public static BigInteger getTypeB(string key)
        {
            return getMedian(key);
        }

        public static Config getStructConfig()
        { 
            byte[] value = Storage.Get(Storage.CurrentContext, GetConfigKey("structConfig".AsByteArray()));
            if (value.Length > 0)
                return Helper.Deserialize(value) as Config;
            return new Config();
        }

        public static bool setStructConfig()
        {  
            Config config = new Config();

            config.liquidate_line_rate_b = getTypeA("liquidate_line_rate_b"); //50
            config.liquidate_line_rate_c = getTypeA("liquidate_line_rate_c"); //150

            config.debt_top_c = getTypeA("debt_top_c"); //1000000000000;

            config.issuing_fee_b = getTypeA("issuing_fee_b"); //1000000000;
            config.liquidate_top_rate_c = getTypeA("liquidate_top_rate_c");// 160;

            config.liquidate_dis_rate_c = getTypeA("liquidate_dis_rate_c"); // 90;
            config.liquidate_line_rateT_c = getTypeA("liquidate_line_rateT_c"); // 120; 

            config.fee_rate_c = getTypeA("fee_rate_c"); //148;

            Storage.Put(Storage.CurrentContext, GetConfigKey("structConfig".AsByteArray()), Helper.Serialize(config));
            
            return true;
        }

        public static BigInteger getMedian(string key)
        {
            return Storage.Get(Storage.CurrentContext, GetMedianKey(key)).AsBigInteger();
        }
        
            public static BigInteger computeMedian(string key)
        {
            Map<byte[], BigInteger> map = getObjectWithKey(key);

            var prices = new BigInteger[map.Values.Length]; 
            {
                int index = 0;

                foreach (BigInteger val in map.Values)
                {
                    if (val > 0)
                    {
                        prices[index] = val;

                        index++;
                    }
                }
            } 

            BigInteger temp;
            for (int i = 0; i < prices.Length; i++)
            {
                for (int j = i; j < prices.Length; j++)
                {
                    if (prices[i] > prices[j])
                    {
                        temp = prices[i];
                        prices[i] = prices[j];
                        prices[j] = temp;
                    }
                }
            }

            BigInteger value = 0;

            if (prices.Length > 0)
            {

                if (prices.Length % 2 != 0)
                {
                    value = prices[(prices.Length + 1) / 2 - 1];
                }
                else
                {
                    int index = prices.Length / 2;

                    value = (prices[index] + prices[index - 1]) / 2;
                }

                Storage.Put(Storage.CurrentContext, GetMedianKey(key), value); 
            }


            return value;
        }



        public class Config
        {
            //B端抵押率   50
        public BigInteger liquidate_line_rate_b;

        //C端抵押率  150
        public BigInteger liquidate_line_rate_c;

        //C端清算折扣  90
        public BigInteger liquidate_dis_rate_c;

        //C端费用率  15秒的费率 乘以10的16次方  66,666,666
        public BigInteger fee_rate_c;

        //C端最高可清算抵押率  160
        public BigInteger liquidate_top_rate_c;

        //C端伺机者可清算抵押率 120
        public BigInteger liquidate_line_rateT_c;

        //C端发行费用 1000
        public BigInteger issuing_fee_c;

        //B端发行费用  1000000000
        public BigInteger issuing_fee_b;

        //C端最大发行量(债务上限)  1000000000000
        public BigInteger debt_top_c;

        }

        public class NodeObj
        {
            //授权的节点地址
            public byte[] addr; 
            //价格/状态
            public BigInteger value;
        }
    }
}

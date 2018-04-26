using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace smartContractDemo
{
    public class SCGetTXInfo:ITest
    {
        string api = "http://192.168.0.101:59908/api/privatenet";

        public string Name => "智能合约查询转账交易";

        public string ID => "SC01";

        async public Task Demo()
        {
            string nnc = "0xeb5e687828caff219738a60f632d4ba08027bf29";
            byte[] prikey = ThinNeo.Helper.GetPrivateKeyFromWIF("KzprnMDQHhK7jnJ3dNNq5C2AfJdy58oGyphnZtc6t78NE26nhq7S");
            byte[] pubkey = ThinNeo.Helper.GetPublicKeyFromPrivateKey(prikey);
            string address = ThinNeo.Helper.GetAddressFromPublicKey(pubkey);
            string toaddr = "APwCdakS1NpJsiq6j9SfvkQFS9ubt347a2";
            string id_GAS = "0x602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7";

            //获取地址的资产列表
            Dictionary<string, List<Utxo>> dir = await Helper.GetBalanceByAddress(api,address);


            string targeraddr = address;  //Transfer it to yourself. 
            ThinNeo.Transaction tran = Helper.makeTran(dir[id_GAS], targeraddr, new ThinNeo.Hash256(id_GAS), decimal.Zero);
            tran.type = ThinNeo.TransactionType.InvocationTransaction;

            ThinNeo.ScriptBuilder sb = new ThinNeo.ScriptBuilder();

            var scriptaddress = new ThinNeo.Hash160(nnc);
            //Parameter inversion 
            MyJson.JsonNode_Array JAParams = new MyJson.JsonNode_Array();
            JAParams.Add(new MyJson.JsonNode_ValueString("(bytes)c19ca691c5a4afb73af23dcdeb2d91a61ae5559d62eac4200a289adb19b9b3b2"));
            sb.EmitParamJson(JAParams);//Parameter list 
            sb.EmitPushString("getTXInfo");//Method
            sb.EmitAppCall(scriptaddress);  //Asset contract 

            ThinNeo.InvokeTransData extdata = new ThinNeo.InvokeTransData();
            extdata.script = sb.ToArray();
            extdata.gas = 1;
            tran.extdata = extdata;

            byte[] msg = tran.GetMessage();
            byte[] signdata = ThinNeo.Helper.Sign(msg, prikey);
            tran.AddWitness(signdata, pubkey, address);
            string txid = tran.GetHash().ToString();
            byte[] data = tran.GetRawData();
            string scripthash = ThinNeo.Helper.Bytes2HexString(data);

            string response = await Helper.HttpGet(api + "?method=sendrawtransaction&id=1&params=[\"" + scripthash + "\"]");
            MyJson.JsonNode_Object resJO = (MyJson.JsonNode_Object)MyJson.Parse(response);
            Console.WriteLine(resJO["result"].ToString());
        }





    }

}

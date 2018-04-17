﻿using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace smartContractDemo
{
    public class SCDemo2:ITest
    {
        string api = "http://192.168.0.101:59908/api/privatenet";

        public string Name => "智能合约3连 2/3";

        public string ID => "SC2/3";
        async public Task Demo()
        {
            string nnc = "0xc5d3b34befad74a8098bb740523c420e3a2e3a11".Replace("0x", "");
            string script = null;
            using (var sb = new ThinNeo.ScriptBuilder())
            {

                sb.EmitParamJson(new MyJson.JsonNode_Array());//参数倒序入
                sb.EmitParamJson(new MyJson.JsonNode_ValueString("(str)name"));//参数倒序入
                ThinNeo.Hash160 shash = new ThinNeo.Hash160(nnc);
                sb.EmitAppCall(shash);//nep5脚本

                sb.EmitParamJson(new MyJson.JsonNode_Array());
                sb.EmitParamJson(new MyJson.JsonNode_ValueString("(str)symbol"));
                sb.EmitAppCall(shash);

                sb.EmitParamJson(new MyJson.JsonNode_Array());
                sb.EmitParamJson(new MyJson.JsonNode_ValueString("(str)decimals"));
                sb.EmitAppCall(shash);

                sb.EmitParamJson(new MyJson.JsonNode_Array());
                sb.EmitParamJson(new MyJson.JsonNode_ValueString("(str)totalSupply"));
                sb.EmitAppCall(shash);


                var data = sb.ToArray();
                script = ThinNeo.Helper.Bytes2HexString(data);

            }

            //var url = Helper.MakeRpcUrl(api, "invokescript", new MyJson.JsonNode_ValueString(script));
            //string result = await Helper.HttpGet(url);

            byte[] postdata;
            var url = Helper.MakeRpcUrlPost(api, "invokescript", out postdata, new MyJson.JsonNode_ValueString(script));
            var result = await Helper.HttpPost(url, postdata);

            Console.WriteLine("得到的结果是：" + result);

        }
    }
}

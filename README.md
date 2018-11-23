# SAR-4C

## Release Note：

**1.0.0**

Script Hash : 

SAR Contract Address: 

## 接口介绍

 方法  | 参数 | 返回值 | 描述 |
--- | --- | --- | ---
openSAR4C | byte[]=>addr、string=>assetType |bool|由地址和抵押资产类型来创建SAR，目前资产类型：cneo_price  '一个地址只能创建一个SAR'
getSAR4C | byte[]=>addr|[SARInfo](#/)|根据地址获取当前SAR信息
reserve | byte[]=>addr、int=>mount|bool|抵押相应数额的资产到SAR，如CNEO资产，mount是CNEO金额
recharge | byte[]=>addr、int=>mount|bool|充值SDS手续费进入到SAR，mount是SDS金额
expande | byte[]=>addr、int=>mount|bool|发行稳定币SDUSD，mount是SDUSD金额
withdraw | byte[]=>addr、int=>mount|bool|提取未发行稳定币的抵押物CNEO，mount是CNEO金额
contract | byte[]=>addr、int=>mount|bool|回收已发行的稳定币SDUSD，mount是SDUSD金额
withdrawT | byte[]=>addr、int=>mount|bool|Bond机制下回收已发行的Bond，mount是Bond金额
rescue | byte[]=>otherAddr、byte[]=>addr、int=>mount|bool|清算其它SAR，mount是SDUSD金额 'SAR抵押率低于150%时候可以进行清算，清算可拿到优惠的CNEO'
rescueT | byte[]=>otherAddr、byte[]=>addr、int=>mount|bool|Bond机制下清算其它SAR回收已发行的稳定币，mount是Bond金额
close | byte[]=>addr|bool|关闭SAR
claimFee | byte[]=>addr、int=>mount|bool|赎回充值的手续费SDS
claimAllFee | byte[]=>addr|bool|赎回所有充值的手续费SDS
migrateSAR4C | byte[]=>addr|bool|迁移SAR至新合约 '包括合约中CNEO转移'
forceMigrate | byte[]=>otherAddr、byte[]=>addr|bool|强制迁移SAR至新合约 '包括合约中CNEO转移 抵押率低于150%才可以触发'
setAccount | string=>key、byte[]=>addr | bool | 设置合约中参数
setBondAccount | byte[]=>addr | bool | 设置Bond中参数
removeBondAccount | byte[]=>addr | bool | 删除Bond中参数
setConfig| string=>key、byte[]=>addr | bool | 增加SAR配置参数

## SARInfo

            //创建者
            public byte[] owner;

            //交易序号
            public byte[] txid;

            //抵押资产,如CNEO
            public BigInteger locked;

            //已发行资产，如SDUSDT  
            public BigInteger hasDrawed;

            //neo:cneo_price   gas:cgas_price 
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
           
           
# Oracle

## Release Note：

**1.0.0**

Script Hash : 

SAR Contract Address: 

## 接口介绍

 方法  | 参数 | 返回值 | 描述 |
--- | --- | --- | --- 
setTypeA | byte[]=>addr、int=>value | bool | 设置(B端C端)全局config参数;设置(B端)锚定物白名单;
getTypeA | byte[]=>addr | int | 获取(B端C端)全局config参数;获取(B端)锚定物是否设置白名单;
setAccount | string=>key、byte[]=>addr | bool | 设置合约中参数
getAccount | string=>key | byte[] | 获取合约中参数
addParaAddrWhit | string=>key、byte[]=>addr、int=>value | bool | 对key添加一个授权节点Addr
removeParaAddrWhit | string=>key、byte[]=>addr | bool | 对key移除一个授权节点Addr
setTypeB | string=>key、byte[]=>addr、int=>value | bool | 设置数字资产价格($);设置锚定物对应美元汇率($)
getTypeB | string=>key | int | 获取多节点取中位数之后的数字资产和锚定物价格($)
setStructConfig | - | bool | 全局配置对象Config赋值存储
getStructConfig | - | Config | 获取全局配置对象Config
getApprovedAddrs | string=>key | object(NodeObj[]) | 根据key查询已授权喂价器地址和状态
getAddrWithParas | string=>key | object(NodeObj[]) | 根据key查询喂价器地址和价格

## NodeObj

            //授权的节点地址
            public byte[] addr; 
            
            //价格/状态
            public BigInteger value;

## Config
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


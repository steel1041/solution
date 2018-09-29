# SAR-4C

## Release Note：

**1.0.0**

Script Hash : 

SAR Contract Address: 

## 接口介绍

 方法  | 参数 | 返回值 | 描述 |
--- | --- | --- | ---
openSAR4C | byte[]=>addr、string=>assetType |bool|由地址和抵押资产类型来创建SAR，目前资产类型：cneo_price  ==一个地址只能创建一个SAR==
getSAR4C | byte[]=>addr|[SARInfo](#/)|根据地址获取当前SAR信息
reserve | byte[]=>addr、int=>mount|bool|抵押相应数额的资产到SAR，如CNEO资产，mount是CNEO金额
recharge | byte[]=>addr、int=>mount|bool|充值SDS手续费进入到SAR，mount是SDS金额
expande | byte[]=>addr、int=>mount|bool|发行稳定币SDUSD，mount是SDUSD金额
withdraw | byte[]=>addr、int=>mount|bool|提取未发行稳定币的抵押物CNEO，mount是CNEO金额
contract | byte[]=>addr、int=>mount|bool|回收已发行的稳定币SDUSD，mount是SDUSD金额
withdrawT | byte[]=>addr、int=>mount|bool|Bond机制下回收已发行的Bond，mount是Bond金额
rescue | byte[]=>otherAddr、byte[]=>addr、int=>mount|bool|清算其它SAR，mount是SDUSD金额 ==SAR抵押率低于150%时候可以进行清算，清算可拿到优惠的CNEO==
rescueT | byte[]=>otherAddr、byte[]=>addr、int=>mount|bool|Bond机制下清算其它SAR回收已发行的稳定币，mount是Bond金额
close | byte[]=>addr|bool|关闭SAR
claimFee | byte[]=>addr|bool|赎回充值的手续费SDS
migrateSAR4C | byte[]=>addr|bool|迁移SAR至新合约 ==包括合约中CNEO转移==
forceMigrate | byte[]=>otherAddr、byte[]=>addr|bool|强制迁移SAR至新合约 ==包括合约中CNEO转移 抵押率低于150%才可以触发==
setAccount | string=>key、byte[]=>addr | bool | 设置合约中参数
setBondAccount | byte[]=>addr | bool | 设置Bond中参数
removeBondAccount | byte[]=>addr | bool | 删除Bond中参数
setConfig| string=>key、byte[]=>addr | bool | 增加SAR配置参数

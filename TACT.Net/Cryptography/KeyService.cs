using System.Collections.Generic;
using TACT.Net.Common;

namespace TACT.Net.Cryptography
{
    public sealed class KeyService
    {
        #region Methods

        public static readonly Salsa20 Salsa20 = new Salsa20();

        public static bool ContainsKey(ulong name) => Keys.ContainsKey(name);

        public static bool TryGetKey(ulong name, out byte[] key) => Keys.TryGetValue(name, out key);

        public static bool TryAddKey(ulong name, string key) => TryAddKey(name, key.ToByteArray());

        public static bool TryAddKey(ulong name, byte[] key)
        {
            if (key.Length != 16)
                return false;

            return Keys.TryAdd(name, key);
        }

        #endregion

        #region Keys

        private static Dictionary<ulong, byte[]> Keys = new Dictionary<ulong, byte[]>()
        {
            { 0xFA505078126ACB3E, "BDC51862ABED79B2DE48C8E7E66C6200".ToByteArray() }, // WOW-20740patch7.0.1_Beta  not used between 7.0 and 7.3
            { 0xFF813F7D062AC0BC, "AA0B5C77F088CCC2D39049BD267F066D".ToByteArray() }, // WOW-20740patch7.0.1_Beta  not used between 7.0 and 7.3
            { 0xD1E9B5EDF9283668, "8E4A2579894E38B4AB9058BA5C7328EE".ToByteArray() }, // WOW-20740patch7.0.1_Beta  Enchanted Torch pet
            { 0xB76729641141CB34, "9849D1AA7B1FD09819C5C66283A326EC".ToByteArray() }, // WOW-20740patch7.0.1_Beta  Enchanted Pen pet
            { 0xFFB9469FF16E6BF8, "D514BD1909A9E5DC8703F4B8BB1DFD9A".ToByteArray() }, // WOW-20740patch7.0.1_Beta  not used between 7.0 and 7.3
            { 0x23C5B5DF837A226C, "1406E2D873B6FC99217A180881DA8D62".ToByteArray() }, // WOW-20740patch7.0.1_Beta  Enchanted Cauldron pet
            //{ 0x3AE403EF40AC3037, "????????????????????????????????".ToByteArray() }, // WOW-21249patch7.0.3_Beta  not used between 7.0 and 7.3
            { 0xE2854509C471C554, "433265F0CDEB2F4E65C0EE7008714D9E".ToByteArray() }, // WOW-21249patch7.0.3_Beta  Warcraft movie items
            { 0x8EE2CB82178C995A, "DA6AFC989ED6CAD279885992C037A8EE".ToByteArray() }, // WOW-21531patch7.0.3_Beta  BlizzCon 2016 Murlocs
            { 0x5813810F4EC9B005, "01BE8B43142DD99A9E690FAD288B6082".ToByteArray() }, // WOW-21531patch7.0.3_Beta  Fel Kitten
            { 0x7F9E217166ED43EA, "05FC927B9F4F5B05568142912A052B0F".ToByteArray() }, // WOW-21531patch7.0.3_Beta  Legion music 
            { 0xC4A8D364D23793F7, "D1AC20FD14957FABC27196E9F6E7024A".ToByteArray() }, // WOW-21691patch7.0.3_Beta  Demon Hunter #1 cinematic (legion_dh1)
            { 0x40A234AEBCF2C6E5, "C6C5F6C7F735D7D94C87267FA4994D45".ToByteArray() }, // WOW-21691patch7.0.3_Beta  Demon Hunter #2 cinematic (legion_dh2)
            { 0x9CF7DFCFCBCE4AE5, "72A97A24A998E3A5500F3871F37628C0".ToByteArray() }, // WOW-21691patch7.0.3_Beta  Val'sharah #1 cinematic (legion_val_yd)
            { 0x4E4BDECAB8485B4F, "3832D7C42AAC9268F00BE7B6B48EC9AF".ToByteArray() }, // WOW-21691patch7.0.3_Beta  Val'sharah #2 cinematic (legion_val_yx)
            { 0x94A50AC54EFF70E4, "C2501A72654B96F86350C5A927962F7A".ToByteArray() }, // WOW-21691patch7.0.3_Beta  Sylvanas warchief cinematic (legion_org_vs)
            { 0xBA973B0E01DE1C2C, "D83BBCB46CC438B17A48E76C4F5654A3".ToByteArray() }, // WOW-21691patch7.0.3_Beta  Stormheim Sylvanas vs Greymane cinematic (legion_sth)
            { 0x494A6F8E8E108BEF, "F0FDE1D29B274F6E7DBDB7FF815FE910".ToByteArray() }, // WOW-21691patch7.0.3_Beta  Harbingers Gul'dan video (legion_hrb_g)
            { 0x918D6DD0C3849002, "857090D926BB28AEDA4BF028CACC4BA3".ToByteArray() }, // WOW-21691patch7.0.3_Beta  Harbingers Khadgar video (legion_hrb_k)
            { 0x0B5F6957915ADDCA, "4DD0DC82B101C80ABAC0A4D57E67F859".ToByteArray() }, // WOW-21691patch7.0.3_Beta  Harbingers Illidan video (legion_hrb_i)
            { 0x794F25C6CD8AB62B, "76583BDACD5257A3F73D1598A2CA2D99".ToByteArray() }, // WOW-21846patch7.0.3_Beta  Suramar cinematic (legion_su_i)
            { 0xA9633A54C1673D21, "1F8D467F5D6D411F8A548B6329A5087E".ToByteArray() }, // WOW-21846patch7.0.3_Beta  legion_su_r cinematic
            { 0x5E5D896B3E163DEA, "8ACE8DB169E2F98AC36AD52C088E77C1".ToByteArray() }, // WOW-21846patch7.0.3_Beta  Broken Shore intro cinematic (legion_bs_i)
            { 0x0EBE36B5010DFD7F, "9A89CC7E3ACB29CF14C60BC13B1E4616".ToByteArray() }, // WOW-21846patch7.0.3_Beta  Alliance Broken Shore cinematic (legion_bs_a)
            { 0x01E828CFFA450C0F, "972B6E74420EC519E6F9D97D594AA37C".ToByteArray() }, // WOW-21846patch7.0.3_Beta  Horde Broken Shore cinematic (legion_bs_h)
            { 0x4A7BD170FE18E6AE, "AB55AE1BF0C7C519AFF028C15610A45B".ToByteArray() }, // WOW-21846patch7.0.3_Beta  Khadgar & Light's Heart cinematic (legion_iq_lv)
            { 0x69549CB975E87C4F, "7B6FA382E1FAD1465C851E3F4734A1B3".ToByteArray() }, // WOW-21846patch7.0.3_Beta  legion_iq_id cinematic
            { 0x460C92C372B2A166, "946D5659F2FAF327C0B7EC828B748ADB".ToByteArray() }, // WOW-21952patch7.0.3_Beta  Stormheim Alliance cinematic (legion_g_a_sth)
            { 0x8165D801CCA11962, "CD0C0FFAAD9363EC14DD25ECDD2A5B62".ToByteArray() }, // WOW-21952patch7.0.3_Beta  Stormheim Horde cinematic (legion_g_h_sth)
            { 0xA3F1C999090ADAC9, "B72FEF4A01488A88FF02280AA07A92BB".ToByteArray() }, // WOW-22578patch7.1.0_PTR   Firecat Mount
            //{ 0x18AFDF5191923610, "????????????????????????????????".ToByteArray() }, // WOW-22578patch7.1.0_PTR   not used between 7.1 and 7.3
            //{ 0x3C258426058FBD93, "????????????????????????????????".ToByteArray() }, // WOW-23436patch7.2.0_PTR   not used between 7.2 and 7.3
            { 0x094E9A0474876B98, "E533BB6D65727A5832680D620B0BC10B".ToByteArray() }, // WOW-23910patch7.2.5_PTR   shadowstalkerpanthermount, shadowstalkerpantherpet
            { 0x3DB25CB86A40335E, "02990B12260C1E9FDD73FE47CBAB7024".ToByteArray() }, // WOW-23789patch7.2.0_PTR   legion_72_ots
            { 0x0DCD81945F4B4686, "1B789B87FB3C9238D528997BFAB44186".ToByteArray() }, // WOW-23789patch7.2.0_PTR   legion_72_tst
            { 0x486A2A3A2803BE89, "32679EA7B0F99EBF4FA170E847EA439A".ToByteArray() }, // WOW-23789patch7.2.0_PTR   legion_72_ars
            { 0x71F69446AD848E06, "E79AEB88B1509F628F38208201741C30".ToByteArray() }, // WOW-24473patch7.3.0_PTR   BlizzCon 2017 Mounts (AllianceShipMount and HordeZeppelinMount)
            { 0x211FCD1265A928E9, "A736FBF58D587B3972CE154A86AE4540".ToByteArray() }, // WOW-24473patch7.3.0_PTR   Shadow fox pet (store) 
            { 0x0ADC9E327E42E98C, "017B3472C1DEE304FA0B2FF8E53FF7D6".ToByteArray() }, // WOW-23910patch7.2.5_PTR   legion_72_tsf
            { 0xBAE9F621B60174F1, "38C3FB39B4971760B4B982FE9F095014".ToByteArray() }, // WOW-24727patch7.3.0_PTR   Rejection of the Gift cinematic (legion_73_agi)
            { 0x34DE1EEADC97115E, "2E3A53D59A491E5CD173F337F7CD8C61".ToByteArray() }, // WOW-24727patch7.3.0_PTR   Resurrection of Alleria Windrunner cinematic (legion_73_avt)
            { 0xE07E107F1390A3DF, "290D27B0E871F8C5B14A14E514D0F0D9".ToByteArray() }, // WOW-25079patch7.3.2_PTR   Tottle battle pet, Raptor mount, Horse mount (104 files)
            { 0x32690BF74DE12530, "A2556210AE5422E6D61EDAAF122CB637".ToByteArray() }, // WOW-24781patch7.3.0_PTR   legion_73_pan
            { 0xBF3734B1DCB04696, "48946123050B00A7EFB1C029EE6CC438".ToByteArray() }, // WOW-25079patch7.3.2_PTR   legion_73_afn
            { 0x74F4F78002A5A1BE, "C14EEC8D5AEEF93FA811D450B4E46E91".ToByteArray() }, // WOW-25079patch7.3.2_PTR   SilithusPhase01 map
            //{ 0x423F07656CA27D23, "????????????????????????????????".ToByteArray() }, // WOW-25600patch7.3.5_PTR   bltestmap
            //{ 0x0691678F83E8A75D, "????????????????????????????????".ToByteArray() }, // WOW-25600patch7.3.5_PTR   filedataid 1782602-1782603
            //{ 0x324498590F550556, "????????????????????????????????".ToByteArray() }, // WOW-25600patch7.3.5_PTR   filedataid 1782615-1782619
            //{ 0xC02C78F40BEF5998, "????????????????????????????????".ToByteArray() }, // WOW-25600patch7.3.5_PTR   test/testtexture.blp (fdid 1782613)
            //{ 0x47011412CCAAB541, "????????????????????????????????".ToByteArray() }, // WOW-25600patch7.3.5_PTR   unused in 25600
            //{ 0x23B6F5764CE2DDD6, "????????????????????????????????".ToByteArray() }, // WOW-25600patch7.3.5_PTR   unused in 25600
            //{ 0x8E00C6F405873583, "????????????????????????????????".ToByteArray() }, // WOW-25600patch7.3.5_PTR   filedataid 1783470-1783472
            { 0x78482170E4CFD4A6, "768540C20A5B153583AD7F53130C58FE".ToByteArray() }, // WOW-25600patch7.3.5_PTR   Magni Bronzebeard VO
            { 0xB1EB52A64BFAF7BF, "458133AA43949A141632C4F8596DE2B0".ToByteArray() }, // WOW-25600patch7.3.5_PTR   dogmount, 50 files
            { 0xFC6F20EE98D208F6, "57790E48D35500E70DF812594F507BE7".ToByteArray() }, // WOW-25632patch7.3.5_PTR   bfa shop stuff
            { 0x402CFABF2020D9B7, "67197BCD9D0EF0C4085378FAA69A3264".ToByteArray() }, // WOW-25678patch7.3.5_PTR   bfa ad texture
            { 0x6FA0420E902B4FBE, "27B750184E5329C4E4455CBD3E1FD5AB".ToByteArray() }, // WOW-25744patch7.3.5_PTR   Legion epilogue cinematics
            { 0x1076074F2B350A2D, "88BF0CD0D5BA159AE7CB916AFBE13865".ToByteArray() }, // WOW-26287patch8.0.1_Beta  skiff
            { 0x816F00C1322CDF52, "6F832299A7578957EE86B7F9F15B0188".ToByteArray() }, // WOW-26287patch8.0.1_Beta  snowkid
            { 0xDDD295C82E60DB3C, "3429CC5927D1629765974FD9AFAB7580".ToByteArray() }, // WOW-26287patch8.0.1_Beta  redbird
            { 0x83E96F07F259F799, "91F7D0E7A02CDE0DE0BD367FABCB8A6E".ToByteArray() }, // WOW-26522patch8.0.1_Beta  BlizzCon 2018 (Alliance and Horde banners and cloaks)
            { 0x49FBFE8A717F03D5, "C7437770CF153A3135FA6DC5E4C85E65".ToByteArray() }, // WOW-27826patch8.1.0_PTR   Meatwagon mount (Warcraft 3: Reforged)
            { 0xC1E5D7408A7D4484, "A7D88E52749FA5459D644523F8359651".ToByteArray() }, // WOW-26871patch8.0.1_Beta  Sylvanas Warbringer cinematic
            { 0xE46276EB9E1A9854, "CCCA36E302F9459B1D60526A31BE77C8".ToByteArray() }, // WOW-26871patch8.0.1_Beta  ltc_a, ltc_h and ltt cinematics
            { 0xD245B671DD78648C, "19DCB4D45A658B54351DB7DDC81DE79E".ToByteArray() }, // WOW-26871patch8.0.1_Beta  stz, zia, kta, jnm & ja cinematics
            { 0x4C596E12D36DDFC3, "B8731926389499CBD4ADBF5006CA0391".ToByteArray() }, // WOW-26871patch8.0.1_Beta  bar cinematic
            { 0x0C9ABD5081C06411, "25A77CD800197EE6A32DD63F04E115FA".ToByteArray() }, // WOW-26871patch8.0.1_Beta  zcf cinematic
            { 0x3C6243057F3D9B24, "58AE3E064210E3EDF9C1259CDE914C5D".ToByteArray() }, // WOW-26871patch8.0.1_Beta  ktf cinematic
            { 0x7827FBE24427E27D, "34A432042073CD0B51627068D2E0BD3E".ToByteArray() }, // WOW-26871patch8.0.1_Beta  rot cinematic
            { 0xFAF9237E1186CF66, "AE787840041E9B4198F479714DAD562C".ToByteArray() }, // WOW-28048patch8.1.0_PTR   encrypted db2 sections (battle pet?)
            //{ 0x5DD92EE32BBF9ABD, "????????????????????????????????".ToByteArray() }, // WOW-27004patch8.0.1_Subm  filedataid 2238294
            { 0x0B68A7AF5F85F7EE, "27AA011082F5E8BBBD71D1BA04F6ABA4".ToByteArray() }, // WOW-28151patch8.1.0_PTR   fdid 2459473, 2459583, 2459601, 2459618, 2459669, 2472143
            //{ 0x01531713C83FCC39, "????????????????????????????????".ToByteArray() }, // WOW-28151patch8.1.0_PTR   fdid 2460009, 2460732
            //{ 0x76E4F6739A35E8D7, "????????????????????????????????".ToByteArray() }, // WOW-28294patch8.1.0_PTR   starts at fdid 2492654, total of 8 fdids
            { 0x66033F28DC01923C, "9F9519861490C5A9FFD4D82A6D0067DB".ToByteArray() }, // WOW-28294patch8.1.0_PTR   vulpine familiar mount
            //{ 0xFCF34A9B05AE7E6A, "????????????????????????????????".ToByteArray() }, // WOW-28151patch8.1.0_PTR   fdid 2468985, 2471011, 2471012, 2471014, 2471016, 2471018
            //{ 0xE2F6BD41298A2AB9, "????????????????????????????????".ToByteArray() }, // WOW-28151patch8.1.0_PTR   fdid 2468988, 2471019, 2471020, 2471021, 2471022, 2471023
            { 0x14C4257E557B49A1, "064A9709F42D50CB5F8B94BC1ACFDD5D".ToByteArray() }, // WOW-28440patch8.1.0_PTR
            { 0x1254E65319C6EEFF, "79D2B3D1CCB015474E7158813864B8E6".ToByteArray() }, // WOW-28440patch8.1.0_PTR
            //{ 0xC8753773ADF1174C, "????????????????????????????????".ToByteArray() }, // WOW-28938patch8.1.5_PTR
            //{ 0x2170BCAA9FA96E22, "????????????????????????????????".ToByteArray() }, // WOW-28938patch8.1.5_PTR
            //{ 0x75485627AA225F4D, "????????????????????????????????".ToByteArray() }, // WOW-28938patch8.1.5_PTR
            //{ 0x08717B15BF3C7955, "????????????????????????????????".ToByteArray() }, // WOW-29220patch8.1.5_PTR   fdid 2823166
        };

        #endregion
    }
}

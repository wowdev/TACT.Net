using System.Collections.Generic;
using TACT.Net.Common;

namespace TACT.Net.Cryptography
{
    /// <summary>
    /// A collection of file encryption keys
    /// <para>See https://wowdev.wiki/TACT#World_of_Warcraft_2</para>
    /// </summary>
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
            { 0xFA505078126ACB3E, "BDC51862ABED79B2DE48C8E7E66C6200".ToByteArray() },
            { 0xFF813F7D062AC0BC, "AA0B5C77F088CCC2D39049BD267F066D".ToByteArray() },
            { 0xD1E9B5EDF9283668, "8E4A2579894E38B4AB9058BA5C7328EE".ToByteArray() },
            { 0xB76729641141CB34, "9849D1AA7B1FD09819C5C66283A326EC".ToByteArray() },
            { 0xFFB9469FF16E6BF8, "D514BD1909A9E5DC8703F4B8BB1DFD9A".ToByteArray() },
            { 0x23C5B5DF837A226C, "1406E2D873B6FC99217A180881DA8D62".ToByteArray() },
            //{ 0x3AE403EF40AC3037, "????????????????????????????????".ToByteArray() },
            { 0xE2854509C471C554, "433265F0CDEB2F4E65C0EE7008714D9E".ToByteArray() },
            { 0x8EE2CB82178C995A, "DA6AFC989ED6CAD279885992C037A8EE".ToByteArray() },
            { 0x5813810F4EC9B005, "01BE8B43142DD99A9E690FAD288B6082".ToByteArray() },
            { 0x7F9E217166ED43EA, "05FC927B9F4F5B05568142912A052B0F".ToByteArray() },
            { 0xC4A8D364D23793F7, "D1AC20FD14957FABC27196E9F6E7024A".ToByteArray() },
            { 0x40A234AEBCF2C6E5, "C6C5F6C7F735D7D94C87267FA4994D45".ToByteArray() },
            { 0x9CF7DFCFCBCE4AE5, "72A97A24A998E3A5500F3871F37628C0".ToByteArray() },
            { 0x4E4BDECAB8485B4F, "3832D7C42AAC9268F00BE7B6B48EC9AF".ToByteArray() },
            { 0x94A50AC54EFF70E4, "C2501A72654B96F86350C5A927962F7A".ToByteArray() },
            { 0xBA973B0E01DE1C2C, "D83BBCB46CC438B17A48E76C4F5654A3".ToByteArray() },
            { 0x494A6F8E8E108BEF, "F0FDE1D29B274F6E7DBDB7FF815FE910".ToByteArray() },
            { 0x918D6DD0C3849002, "857090D926BB28AEDA4BF028CACC4BA3".ToByteArray() },
            { 0x0B5F6957915ADDCA, "4DD0DC82B101C80ABAC0A4D57E67F859".ToByteArray() },
            { 0x794F25C6CD8AB62B, "76583BDACD5257A3F73D1598A2CA2D99".ToByteArray() },
            { 0xA9633A54C1673D21, "1F8D467F5D6D411F8A548B6329A5087E".ToByteArray() },
            { 0x5E5D896B3E163DEA, "8ACE8DB169E2F98AC36AD52C088E77C1".ToByteArray() },
            { 0x0EBE36B5010DFD7F, "9A89CC7E3ACB29CF14C60BC13B1E4616".ToByteArray() },
            { 0x01E828CFFA450C0F, "972B6E74420EC519E6F9D97D594AA37C".ToByteArray() },
            { 0x4A7BD170FE18E6AE, "AB55AE1BF0C7C519AFF028C15610A45B".ToByteArray() },
            { 0x69549CB975E87C4F, "7B6FA382E1FAD1465C851E3F4734A1B3".ToByteArray() },
            { 0x460C92C372B2A166, "946D5659F2FAF327C0B7EC828B748ADB".ToByteArray() },
            { 0x8165D801CCA11962, "CD0C0FFAAD9363EC14DD25ECDD2A5B62".ToByteArray() },
            { 0xA3F1C999090ADAC9, "B72FEF4A01488A88FF02280AA07A92BB".ToByteArray() },
            //{ 0x18AFDF5191923610, "????????????????????????????????".ToByteArray() },
            //{ 0x3C258426058FBD93, "????????????????????????????????".ToByteArray() },
            { 0x094E9A0474876B98, "E533BB6D65727A5832680D620B0BC10B".ToByteArray() },
            { 0x3DB25CB86A40335E, "02990B12260C1E9FDD73FE47CBAB7024".ToByteArray() },
            { 0x0DCD81945F4B4686, "1B789B87FB3C9238D528997BFAB44186".ToByteArray() },
            { 0x486A2A3A2803BE89, "32679EA7B0F99EBF4FA170E847EA439A".ToByteArray() },
            { 0x71F69446AD848E06, "E79AEB88B1509F628F38208201741C30".ToByteArray() },
            { 0x211FCD1265A928E9, "A736FBF58D587B3972CE154A86AE4540".ToByteArray() },
            { 0x0ADC9E327E42E98C, "017B3472C1DEE304FA0B2FF8E53FF7D6".ToByteArray() },
            { 0xBAE9F621B60174F1, "38C3FB39B4971760B4B982FE9F095014".ToByteArray() },
            { 0x34DE1EEADC97115E, "2E3A53D59A491E5CD173F337F7CD8C61".ToByteArray() },
            { 0xE07E107F1390A3DF, "290D27B0E871F8C5B14A14E514D0F0D9".ToByteArray() },
            { 0x32690BF74DE12530, "A2556210AE5422E6D61EDAAF122CB637".ToByteArray() },
            { 0xBF3734B1DCB04696, "48946123050B00A7EFB1C029EE6CC438".ToByteArray() },
            { 0x74F4F78002A5A1BE, "C14EEC8D5AEEF93FA811D450B4E46E91".ToByteArray() },
            //{ 0x423F07656CA27D23, "????????????????????????????????".ToByteArray() },
            //{ 0x0691678F83E8A75D, "????????????????????????????????".ToByteArray() },
            //{ 0x324498590F550556, "????????????????????????????????".ToByteArray() },
            //{ 0xC02C78F40BEF5998, "????????????????????????????????".ToByteArray() },
            //{ 0x47011412CCAAB541, "????????????????????????????????".ToByteArray() },
            //{ 0x23B6F5764CE2DDD6, "????????????????????????????????".ToByteArray() },
            //{ 0x8E00C6F405873583, "????????????????????????????????".ToByteArray() },
            { 0x78482170E4CFD4A6, "768540C20A5B153583AD7F53130C58FE".ToByteArray() },
            { 0xB1EB52A64BFAF7BF, "458133AA43949A141632C4F8596DE2B0".ToByteArray() },
            { 0xFC6F20EE98D208F6, "57790E48D35500E70DF812594F507BE7".ToByteArray() },
            { 0x402CFABF2020D9B7, "67197BCD9D0EF0C4085378FAA69A3264".ToByteArray() },
            { 0x6FA0420E902B4FBE, "27B750184E5329C4E4455CBD3E1FD5AB".ToByteArray() },
            { 0x1076074F2B350A2D, "88BF0CD0D5BA159AE7CB916AFBE13865".ToByteArray() },
            { 0x816F00C1322CDF52, "6F832299A7578957EE86B7F9F15B0188".ToByteArray() },
            { 0xDDD295C82E60DB3C, "3429CC5927D1629765974FD9AFAB7580".ToByteArray() },
            { 0x83E96F07F259F799, "91F7D0E7A02CDE0DE0BD367FABCB8A6E".ToByteArray() },
            { 0x49FBFE8A717F03D5, "C7437770CF153A3135FA6DC5E4C85E65".ToByteArray() },
            { 0xC1E5D7408A7D4484, "A7D88E52749FA5459D644523F8359651".ToByteArray() },
            { 0xE46276EB9E1A9854, "CCCA36E302F9459B1D60526A31BE77C8".ToByteArray() },
            { 0xD245B671DD78648C, "19DCB4D45A658B54351DB7DDC81DE79E".ToByteArray() },
            { 0x4C596E12D36DDFC3, "B8731926389499CBD4ADBF5006CA0391".ToByteArray() },
            { 0x0C9ABD5081C06411, "25A77CD800197EE6A32DD63F04E115FA".ToByteArray() },
            { 0x3C6243057F3D9B24, "58AE3E064210E3EDF9C1259CDE914C5D".ToByteArray() },
            { 0x7827FBE24427E27D, "34A432042073CD0B51627068D2E0BD3E".ToByteArray() },
            { 0xFAF9237E1186CF66, "AE787840041E9B4198F479714DAD562C".ToByteArray() },
            //{ 0x5DD92EE32BBF9ABD, "????????????????????????????????".ToByteArray() },
            { 0x0B68A7AF5F85F7EE, "27AA011082F5E8BBBD71D1BA04F6ABA4".ToByteArray() },
            //{ 0x01531713C83FCC39, "????????????????????????????????".ToByteArray() },
            { 0x76E4F6739A35E8D7, "05CF276722E7165C5A4F6595256A0BFB".ToByteArray() },
            { 0x66033F28DC01923C, "9F9519861490C5A9FFD4D82A6D0067DB".ToByteArray() },
            { 0xFCF34A9B05AE7E6A, "E7C2C8F77E30AC240F39EC23971296E5".ToByteArray() },
            { 0xE2F6BD41298A2AB9, "C5DC1BB43B8CF3F085D6986826B928EC".ToByteArray() },
            { 0x14C4257E557B49A1, "064A9709F42D50CB5F8B94BC1ACFDD5D".ToByteArray() },
            { 0x1254E65319C6EEFF, "79D2B3D1CCB015474E7158813864B8E6".ToByteArray() },
            { 0xC8753773ADF1174C, "1E0E37D42EE5CE5E8067F0394B0905F2".ToByteArray() },
            //{ 0x2170BCAA9FA96E22, "????????????????????????????????".ToByteArray() },
            //{ 0x75485627AA225F4D, "????????????????????????????????".ToByteArray() },
            { 0x08717B15BF3C7955, "4B06BF9D17663CEB3312EA3C69FBC5DD".ToByteArray() },
            //{ 0xD19DCF7ACA8D96D6, "????????????????????????????????".ToByteArray() },
            { 0x9FD609902B4B2E07, "ABE0C5F9C123E6E24E7BEA43C2BF00AC".ToByteArray() },
            //{ 0xCB26B441FAE4C8CD, "????????????????????????????????".ToByteArray() },
            //{ 0xA98C7594F55C02F0, "????????????????????????????????".ToByteArray() },
            { 0x259EE68CD9E76DBA, "465D784F1019661CCF417FE466801283".ToByteArray() },
            //{ 0x6A026290FBDB3754, "????????????????????????????????".ToByteArray() },
            { 0xCF72FD04608D36ED, "A0A889976D02FA8D00F7AF0017AD721F".ToByteArray() },
            { 0x17F07C2E3A45DB3D, "6D3886BDB91E715AE7182D9F3A08F2C9".ToByteArray() },
            //{ 0xDFAB5841B87802B5, "????????????????????????????????".ToByteArray() },
            { 0xC050FA06BB0538F6, "C552F5D0B72231502D2547314E6015F7".ToByteArray() },
            { 0xAB5CDD3FC321831F, "E1384F5B06EBBCD333695AA6FFC68318".ToByteArray() },
            { 0xA7B7D1F12395040E, "36AD3B31273F1EBCEE8520AAA74B12F2".ToByteArray() },
            //{ 0x83A2AB72DD8AE992, "????????????????????????????????".ToByteArray() },
            //{ 0xBEAF567CC45362F0, "????????????????????????????????".ToByteArray() },
            //{ 0x7BB3A77FD8D14783, "????????????????????????????????".ToByteArray() },
            //{ 0x8F4098E2470FE0C8, "????????????????????????????????".ToByteArray() },
            //{ 0x6AC5C837A2027A6B, "????????????????????????????????".ToByteArray() },
            //{ 0x302AAD8B1F441D95, "????????????????????????????????".ToByteArray() },
            //{ 0x5C909F00088734B9, "????????????????????????????????".ToByteArray() },
            //{ 0xF785977C76DE9C77, "????????????????????????????????".ToByteArray() },
            //{ 0x1CDAF3931871BEC3, "????????????????????????????????".ToByteArray() },
        };

        #endregion
    }
}

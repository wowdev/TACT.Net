using System;

namespace TACT.Net.Root
{
    [Flags]
    public enum LocaleFlags
    {
        None = 0x0,
        enUS = 0x2,
        koKR = 0x4,
        frFR = 0x10,
        deDE = 0x20,
        zhCN = 0x40,
        esES = 0x80,
        zhTW = 0x100,
        enGB = 0x200,
        enCN = 0x400,
        enTW = 0x800,
        esMX = 0x1000,
        ruRU = 0x2000,
        ptBR = 0x4000,
        itIT = 0x8000,
        ptPT = 0x10000,
        All_Classic_WoW = enUS | koKR | frFR | deDE | zhCN | esES | zhTW | enGB | esMX | ruRU | ptBR | ptPT,
        All_WoW = enUS | koKR | frFR | deDE | zhCN | esES | zhTW | enGB | esMX | ruRU | ptBR | itIT | ptPT,
    }
}

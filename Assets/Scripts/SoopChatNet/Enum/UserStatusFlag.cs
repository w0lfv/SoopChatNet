using System;

namespace SoopChatNet
{
    [Flags]
    public enum UserStatusFlag1
    {
        NONE = 0,
        ADMIN = 1 << 0, // = 1
        HIDDEN = 1 << 1, // = 2
        BJ = 1 << 2, // = 4
        DUMB = 1 << 3, // = 8
        GUEST = 1 << 4, // = 16
        FANCLUB = 1 << 5, // = 32
        AUTOMANAGER = 1 << 6, // = 64
        MANAGERLIST = 1 << 7, // = 128
        MANAGER = 1 << 8, // = 256
        FEMALE = 1 << 9, // = 512
        AUTODUMB = 1 << 10, // = 1024
        DUMB_BLIND = 1 << 11, // = 2048
        DOBAE_BLIND = 1 << 12, // = 4096
        DOBAE_BLIND2 = 1 << 24, // = 16777216
        EXITUSER = 1 << 13, // = 8192
        MOBILE = 1 << 14, // = 16384
        TOPFAN = 1 << 15, // = 32768
        REALNAME = 1 << 16, // = 65536
        NODIRECT = 1 << 17, // = 131072
        GLOBAL_APP = 1 << 18, // = 262144
        QUICKVIEW = 1 << 19, // = 524288
        SPTR_STICKER = 1 << 20, // = 1048576
        CHROMECAST = 1 << 21, // = 2097152
        MOBILE_WEB = 1 << 23, // = 8388608
        FOLLOWER = 1 << 28, // = 268435456
        NOTIVODBALLOON = 1 << 30, // = 1073741824
        NOTITOPFAN = 1 << 31, // = 2147483648
    }

    [Flags]
    public enum UserStatusFlag2
    {
        NONE = 0,
        GLOBAL_PC = 1 << 0, // = 1
        CLAN = 1 << 1, // = 2
        TOPCLAN = 1 << 2, // = 4
        TOP20 = 1 << 3, // = 8
        GAMEGOD = 1 << 4, // = 16
        ATAG_ALLOW = 1 << 5, // = 32
        NOSUPERCHAT = 1 << 6, // = 64
        NORECVCHAT = 1 << 7, // = 128
        FLASH = 1 << 8, // = 256
        LGGAME = 1 << 9, // = 512
        EMPLOYEE = 1 << 10, // = 1024
        CLEANATI = 1 << 11, // = 2048
        POLICE = 1 << 12, // = 4096
        ADMINCHAT = 1 << 13, // = 8192
        PC = 1 << 14, // = 16384
        SPECIFY = 1 << 15, // = 32768

        // Below here are not exist in Soop SDK
        NEW_STUDIO = 1 << 16, // = 65536
        HTML5 = 1 << 17, // = 131072
        FOLLOWER_PERIOD_3 = 1 << 23, // = 8388608
        FOLLOWER_PERIOD_6 = 1 << 18, // = 262144
        FOLLOWER_PERIOD_12 = 1 << 19, // = 524288
        FOLLOWER_PERIOD_24 = 1 << 20, // = 1048576
        FOLLOWER_PERIOD_36 = 1 << 24, // = 16777216
        HIDE_SEX = 1 << 25, // = 33554432
    }
}

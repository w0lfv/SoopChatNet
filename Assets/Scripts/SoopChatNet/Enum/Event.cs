namespace SoopChatNet
{
    public enum Event
    {
        JOIN, // User joined / SVC_LOGIN
        QUIT, // User left / SVC_QUITCH
        IN, // Other user joined / SVC_CHUSER_EXTEND
        OUT, // Other user left / SVC_CHUSER
        USERSTATUS_CHANGED, // SVC_SETUSERFLAG
        MESSAGE, // User's chat / SVC_CHATMESG
        MANAGER_MESSAGE, // Manager's chat / SVC_MANAGERCHAT
        CHAT_MUTED, // User muted / SVC_SETDUMB
        BANNED, // User banned / SVC_KICK_AND_CANCEL
        BAN_REVOKED, // User unbanned / SVC_KICK_AND_CANCEL
        BANNED_USER_LIST, // List of banned user / SVC_KICK_USERLIST
        MANAGER_APPOINTMENT, // User manager status changed / SVC_SETSUBBJ
        BALLOON_GIFTED, // User donated with Balloon / SVC_SENDBALLOON / SVC_SENDBALLOONSUB / SVC_VODBALLOON
        STICKER_GIFTED, // User donated with Sticker / SVC_SENDFANLETTER / SVC_SENDFANLETTERSUB
        ADBALLOON_GIFTED, // User donated with ADBallon / SVC_VODADCON / SVC_STATION_ADCON / SVC_ADCON_EFFECT
        VIDEOBALLOON_GIFTED, // User donated with VideoBalloon / SVC_VIDEO_BALLOON
        QUICKVIEW_GIFTED, // User gifted Quickview / SVC_SENDQUICKVIEW
        OGQ_EMOTICON_GIFTED, // User gifted OGQ Emoticon / SVC_OGQ_EMOTICON_GIFT
        SUBSCRIBED, // User subscribed to the streamer / SVC_FOLLOW_ITEM
        SUBSCRIPTION_RENEWED, // User renewed subscription / SVC_FOLLOW_ITEM_EFFECT
        SUBSCRIPTION_GIFTED, // User gifted subscription / SVC_SENDSUBSCRIPTION
        CHAT_FREEZE, // Chat freeze status changed / SVC_ICEMODE_EX
        POLL, // Polling status changed / SVC_NOTIFY_POLL
        BJ_NOTICE, // Streamer's notice / SVC_BJ_NOTICE
        ITEM_DROPS, // Drops! / SVC_ITEM_DROPS
        BREAK_TIME, // Breaktime status changed / SVC_AD_IN_BROAD_JSON
        GEM_GIFTED, // GEM / SVC_GEM_ITEMSEND
        BATTLE_MISSION_GIFTED, // Battle mission donation started / SVC_MISSION
        BATTLE_MISSION_FINISHED, // Battle mission donation finished / SVC_MISSION
        BATTLE_MISSION_SETTLED, // Battle mission donation distribution / SVC_MISSION
        CHALLENGE_MISSION_GIFTED, // Challenge donation started / SVC_MISSION
        CHALLENGE_MISSION_FINISHED, // Challenge donation finished / SVC_MISSION
        CHALLENGE_MISSION_SETTLED, // Challenge donation giveaway / SVC_MISSION
        CHALLENGE_MISSION_SPONSORS, // List of Challenge donation / SVC_MISSION_SETTLE
        SLOW_MODE, // Chat slow mode / SVC_SLOWMODE
    }
}
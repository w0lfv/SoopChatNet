using System.Collections.Generic;

namespace SoopChatNet.Data
{
    [System.Serializable]
    public class ChannelData
    {
        public Channel channel;
        [System.Serializable]
        public class Channel
        {
            public string geo_cc;
            public string geo_rc;
            public string acpt_lang;
            public string svc_lang;
            public int ISSP;
            public int LOWLAYTENCYBJ;
            public List<ViewPreset> VIEWPRESET;
            [System.Serializable]
            public class ViewPreset
            {
                public string label;
                public string label_resolution;
                public string name;
                public int bps;
            }
            public int RESULT;
            public string PBNO;
            public string BNO;
            public string BJID;
            public string BJNICK;
            public int BJGRADE;
            public string STNO;
            public string ISFAV;
            public string CATE;
            public int CPNO;
            public string GRADE;
            public string BTYPE;
            public string CHATNO;
            public string BPWD;
            public string TITLE;
            public string BPS;
            public string RESOLUTION;
            public string CTIP;
            public string CTPT;
            public string VBT;
            public int CTUSER;
            public int S1440P;
            public List<string> AUTO_HASHTAGS;
            public List<string> CATEGORY_TAGS;
            public List<string> HASH_TAGS;
            public string CHIP;
            public string CHPT;
            public string CHDOMAIN;
            public string CDN;
            public string RMD;
            public string GWIP;
            public string GWPT;
            public string STYPE;
            public string ORG;
            public string MDPT;
            public int BTIME;
            public int DH;
            public int WC;
            public PconObject PCON_OBJECT;
            [System.Serializable]
            public class PconObject
            {
                public List<PconTier> tier1;
                public List<PconTier> tier2;
                [System.Serializable]
                public class PconTier
                {
                    public int MONTH;
                    public string FILENAME;
                }
            }
            public string FTK;
            public bool BPCBANNER;
            public bool BPCCHATPOPBANNER;
            public bool BPCTIMEBANNER;
            public bool BPCCONNECTBANNER;
            public bool BPCLOADINGBANNER;
            public string BPCPOSTROLL;
            public string BPCPREROLL;
            public Midroll MIDROLL;
            [System.Serializable]
            public class Midroll
            {
                public string VALUE;
                public int OFFSET_START_TIME;
                public int OFFSET_END_TIME;
            }
            public string PREROLLTAG;
            public string MIDROLLTAG;
            public string POSTROLLTAG;
            public bool BJAWARD;
            public bool BJAWARDWATERMARK;
            public string BJAWARDYEAR;
            public bool GEM;
            public bool GEM_LOG;
            public List<string> CLEAR_MODE_CATE;
            public string PLAYTIMINGBUFFER_DURATION;
            public string STREAMER_PLAYTIMINGBUFFER_DURATION;
            public string MAXBUFFER_DURATION;
            public string LOWBUFFER_DURATION;
            public string PLAYBACKRATEDELTA;
            public string MAXOVERSEEKDURATION;
            public string TIER1_NICK;
            public string TIER2_NICK;
            public int EXPOST_FLAG;
            public int SUB_PAY_CNT;
            public bool SAVVY_ALLOWED;
        }
    }
}

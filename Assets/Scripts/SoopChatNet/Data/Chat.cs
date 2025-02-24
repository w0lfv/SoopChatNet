namespace SoopChatNet.Data
{
    public class Chat
    {
        public string sender;
        public string nickname;
        public string message;
        public int lang;
        public int subMonth;
        public UserStatusFlag1 flag1;
        public UserStatusFlag2 flag2;

        public Chat(string[] packet)
        {
            message = packet[0].Replace("\r", "");
            sender = packet[1];

            int permission = int.Parse(packet[3]);
            lang = int.Parse(packet[4]);
            nickname = packet[5];
            string flagStr = packet[6];
            subMonth = packet[7] == "-1" ? 0 : int.Parse(packet[7]);
            //Packet[8] = Color

            if (permission == 3 || permission == 0)
            {
                string[] flags = flagStr.Split('|');
                flag1 = (UserStatusFlag1)long.Parse(flags[0]);
                flag2 = (UserStatusFlag2)long.Parse(flags[1]);
            }
            else
            {
                flag1 = UserStatusFlag1.NONE;
                flag2 = UserStatusFlag2.NONE;
            }
        }
    }
}

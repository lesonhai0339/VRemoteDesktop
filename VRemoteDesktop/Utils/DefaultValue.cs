using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Utils
{
    public static class ByteConstants
    {
        public static int INT32_LENGTH = 4;
        public static int INT64_LENGTH = 8;
    }
    public static class RandomLength
    {
        public static int DATA_TYPE_LENGTH = 1;
        public static int ID_LENGTH = 8;
        public static int PASSWORD_LENGTH = 4;
        public static int SOCKET_ID_LENGTH = 8;
        public static int FILE_ID_LENGTH = 16;
        public static int RANDOM_STRING_LENGTH = 8;
        public static int RANDOM_GUILD_LENGTH = 16;
    }
    public static class DefaultSocketPacket
    {
        public static int DEFAULT_BUFFER_SIZE = 1024;
        public static int DEFAULT_CHUNK_SIZE = 1024 * 32;
    }
    public static class DefaultScreen
    {
        public static int DEFAULT_BLOCK_SIZE = 64;
        public static int DEFAULT_FPS = 20;
    }
    public static class DefaultChat
    {
        public static string DEFAULT_CHAT_FOLDER = "ChatData";
        public static int DEFAULT_MESSAGE_LOAD = 5;
    }
    public static class DefaultForm
    {
        public static string FORM_COMPLETED = "Hoàn thành";
        public static string FORM_ERROR = "Xảy ra lỗi";
        public static string FORM_STOP = "Dừng";
        public static string FORM_REJECT_FILE = "Đã từ chối file";
        public static string FORM_WAITING = "Vui lòng chờ...";
        public static string FORM_WAITING_PARTNER_ACCEPTED = "Vui lòng đối tác xác nhận...";
        public static string FORM_SUCCESS_TITLE = "Thành công";
        public static string FORM_FAILED_TITLE = "Thất bại";
        public static string FORM_ERROR_TITLE = "Xảy ra lỗi";
        public static string FORM_TIMEOUT_TITLE = "Timeout";
    }
    public static class DefaultValue
    {
        public static string DEFAULT_DATETIME_FORMAT = "yyyy-MM-dd HH:mm:ss";
        public static string DEFAULT_SEPRATOR = "|";
        public static int DEFAULT_TIMEOUT = 30;
    }
}

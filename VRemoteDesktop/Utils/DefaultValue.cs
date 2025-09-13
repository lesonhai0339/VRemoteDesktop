using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Utils
{
    public static class DefaultClientInfo
    {
        public static int CLIENT_INFO_MIN_FIELDS = 10;
        public static int CLIENT_INFO_ID_INDEX = 0;
        public static int CLIENT_INFO_PASSWORD_INDEX = 1;
        public static int CLIENT_INFO_COMPUTER_NAME_INDEX = 2;
        public static int CLIENT_INFO_WIDTH_INDEX = 3;
        public static int CLIENT_INFO_HEIGHT_INDEX = 4;
        public static int CLIENT_INFO_MAJOR_VERSION_INDEX = 5;
        public static int CLIENT_INFO_MINOR_VERSION_INDEX = 6;
        public static int CLIENT_INFO_IP_INDEX = 7;
        public static int CLIENT_INFO_PORT_INDEX = 8;
        public static int CLIENT_INFO_PUBLIC_IP_INDEX = 9;
    }
    public static class DefaultMouse
    {
        public static int MOUSE_MIN_FIELDS = 6;
        public static int MOUSE_PARTNET_WIDTH_INDEX = 0;
        public static int MOUSE_PARTNER_HEIGHT_INDEX = 1;
        public static int MOUSE_MESSAGE = 2;
        public static int MOUSE_ACTION = 3;
        public static int MOUSE_X = 4;
        public static int MOUSE_Y = 5;
    }
    public static class DefaultKeyboard
    {
        public static int KEYBOARD_MIN_FIELDS = 4;
        public static int KEYBOARD_COMMAND_INDEX = 0;
        public static int KEYBOARD_MODIFIER_INDEX = 1;
        public static int KEYBOARD_KEY_INDEX = 2;
        public static int KEYBOARD_TYPE_INDEX = 3;
    }
    public static class DefaultClipboard
    {
        /// <summary>
        /// Maximun characters clipboard can copy
        /// </summary>
        public static int MAX_CLIPBOARD_LENGTH = 1000000;
    }
    public static class DefaultFileInfo
    {
        public static int DEFAULT_CHUNK_FILE_SIZE = 1024 * 32;
        public static int OFFSET_INT32_LENGTH = 4;
        public static int FILE_ID_LENGTH = 16;
        public static int FILE_INFO_MIN_FIELDS = 5;
        public static int FILE_ID_INDEX = 0;
        public static int FILE_NAME_INDEX = 1;
        public static int FILE_EXTENSION_INDEX = 2;
        public static int FILE_SIZE_INDEX = 3;
        public static int FILE_CHECKSUM_INDEX = 4;
    }
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
        public static int DEFAULT_CHUNK_SIZE = 1024 * 8;
        public static int DEFAULT_BLOCK_SIZE = 64;
        public static int DEFAULT_FPS = 20;
        public static int DEFAULT_CHUNK_HEADER_LENGTH = 20;
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
        public static string FORM_STOP = "Đã dừng";
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
        public static int DEFAULT_TIMEOUT_SECONDS = 30;
        public static int DEFAULT_TIMEOUT_MINUTES = 30;
        public static int SHA_CHECKSUM_LENGTH = 40;
    }
}

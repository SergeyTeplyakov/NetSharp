public static class Protocol
{
    public static class Commands
    {
        /// <summary>
        /// �������� �����.
        /// </summary>
        public const int UPLOAD_FILE = 1;
    }

    public static class Errors
    {
        /// <summary>
        /// ��� ������ ����� ��� ������������ �����.
        /// </summary>
        public const int FT_NOT_ENOUGH_SPACE = 1;
        /// <summary>
        /// ����� ������ ������ ��������� � ������� ��� ������� �����.
        /// </summary>
        public const int FT_OTHER_ERROR = 2;
    }
}
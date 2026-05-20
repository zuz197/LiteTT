namespace Lite.Admin
{
    /// <summary>
    /// Lớp biểu diễn kết quả khi gọi Api
    /// </summary>
    public class ApiResult
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="code"></param>
        /// <param name="message"></param>
        public ApiResult(int code, string message ) 
        { 
            Code = code;
            Message = message;     
        }
        /// <summary>
        /// 0: Lỗi / hoặc không thành công, lớn hơn 0: thành công
        /// </summary>
        public int Code { get; set; }
        public string Message { get; set; }
    }
}

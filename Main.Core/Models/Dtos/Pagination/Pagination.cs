namespace Main.Core.Models.Dtos.Pagination
{
    public class Pagination<T>
    {
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int Count { get; set; }
        public int TotalPages { get; set; }
        public IReadOnlyList<T> Data { get; set; }

        public Pagination(int pageIndex, int pageSize, int count, IReadOnlyList<T> data)
        {
            PageIndex = pageIndex;
            PageSize = pageSize;
            Count = count;
            TotalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(count / (double)pageSize);
            Data = data;
        }
    }
}

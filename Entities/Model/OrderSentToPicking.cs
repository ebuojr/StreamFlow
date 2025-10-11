namespace Entities.Model
{
    public class OrderSentToPicking
    {
        public Guid Id { get; set; }
        public int OrderNo { get; set; }
        public DateTime SentTime { get; set; }
    }
}

namespace OrderApi.Model
{
    public class Payment
    {
        public string PaymentMethod { get; set; }
        public string PaymentStatus { get; set; }
        public DateTime PaidAt { get; set; }
        public string TransactionId { get; set; }
        public string Currency { get; set; }
        public decimal Amount { get; set; }
    }
}

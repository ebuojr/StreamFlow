namespace Contracts
{
    public record CreateOrderResponse
    {
        public string OrderNo { get; set; }
        public bool IsSuccessfullyCreated { get; set; }
        public string ErrorMessage { get; set; }
    }
}

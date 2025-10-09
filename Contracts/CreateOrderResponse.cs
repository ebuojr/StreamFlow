namespace Contracts
{
    public record CreateOrderResponse
    {
        public int OrderNo { get; set; }
        public bool IsSuccessfullyCreated { get; set; }
        public string ErrorMessage { get; set; }
    }
}

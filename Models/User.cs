namespace ChessAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public UserData Data { get; set; }
    }
}

namespace NuVatis.Sample.Models;

/**
 * 사용자 모델.
 */
public class User {
    public int Id       { get; set; }
    public string Name  { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age      { get; set; }

    public override string ToString() => $"User(Id={Id}, Name={Name}, Email={Email}, Age={Age})";
}

namespace ClassBook.Application.DTOs
{
    public class CreateClassDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class DeleteClassDto
    {
        public string StudentAction { get; set; } = "keepWithoutClass";
        public int? TargetClassId { get; set; }
    }
}

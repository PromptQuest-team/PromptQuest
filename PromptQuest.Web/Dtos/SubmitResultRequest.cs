namespace PromptQuest.Web.Dtos;

public sealed record CheckResultDto(string Id, bool Passed);

// Длина промта для рекорда считается на сервере из Attempt.Prompt, уже
// сохранённого в хранилище — клиент её не передаёт и не может повлиять на неё.
public sealed record SubmitResultRequest(
    bool Passed,
    string Code,
    List<CheckResultDto>? Checks);

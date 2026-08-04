using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenAI.Chat;
using SerilogTimings;
using Tutor.Api.Models.Constants;
using Tutor.Api.Models.Tutor.Api.Contracts.ChatServices;
using Tutor.Api.Services;

namespace Tutor.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    [EnableRateLimiting("Chat")]
    public class ChatController(ChatGptAudioService chatGptAudioService, ChatGptChatService chatGptChatService, ILogger<ChatController> logger) : ControllerBase
    {
        [HttpPost("speak")]
        public async Task<IActionResult> Speak([FromForm] IFormFile voice)
        {
            string? userId = User.GetClaimValue(TutorClaimTypes.Id);
            using var operation = Operation.Begin("Handle speak chat request for user {UserId}", userId ?? "unknown");

            if (userId is null)
            {
                logger.LogError("The logged in user is not valid! User: {@user}", User);
                operation.Complete("Result", "InvalidUser");
                return BadRequest("The user is not valid!");
            }

            logger.LogInformation(
                "Speak chat request received for user {UserId}; file name {FileName}, content type {ContentType}, size {SizeBytes} bytes.",
                userId,
                voice.FileName,
                voice.ContentType,
                voice.Length);

            if(voice.Length >= Limit.MAX_VOICE_SIZE)
            {
                logger.LogWarning(
                    "Speak chat request rejected for user {UserId}: voice size {SizeBytes} exceeded limit {MaxSizeBytes}.",
                    userId,
                    voice.Length,
                    Limit.MAX_VOICE_SIZE);
                operation.Complete("Result", "VoiceTooLarge");
                return BadRequest($"A voice message size should be less the {Limit.MAX_VOICE_SIZE / 3}MB.");
            }

            string transcription;
            try
            {
                transcription = await chatGptAudioService.Transcribe(voice, userId);
            }
            catch (Exception ex)
            {
                operation.SetException(ex);
                operation.Abandon();
                throw;
            }

            logger.LogInformation(
                "Speak chat request completed for user {UserId}; transcription length {TranscriptionLength}.",
                userId,
                transcription.Length);
            operation.Complete("Result", "Success");
            return Ok(transcription);
        }

        [HttpPost("write")]
        public async Task<IActionResult> Write([FromBody] Message[] tutorChat, CancellationToken token)
        {
            string? userId = User.GetClaimValue(TutorClaimTypes.Id);
            using var operation = Operation.Begin("Handle write chat request for user {UserId}", userId ?? "unknown");

            if( userId is null)
            {
                logger.LogError("The logged in user is not valid! User: {@user}", User);
                operation.Complete("Result", "InvalidUser");
                return BadRequest("The user is not valid!");
            }

            if (tutorChat.Length == 0)
            {
                logger.LogWarning("Write chat request rejected for user {UserId}: request contained no messages.", userId);
                operation.Complete("Result", "NoMessages");
                return BadRequest("Chat messages cannot be null or empty.");
            }

            logger.LogInformation(
                "Write chat request received for user {UserId}; message count {MessageCount}, roles {Roles}, total content length {TotalContentLength}.",
                userId,
                tutorChat.Length,
                string.Join(",", tutorChat.Select(x => x.Role)),
                tutorChat.Sum(x => x.Content.Length));

            if(tutorChat.Any(x => !x.Role.Equals("system") && x.Content.Length >= Limit.MAX_MESSAGE_LENGTH))
            {
                logger.LogWarning(
                    "Write chat request rejected for user {UserId}: at least one message exceeded limit {MaxMessageLength}.",
                    userId,
                    Limit.MAX_MESSAGE_LENGTH);
                operation.Complete("Result", "MessageTooLong");
                return BadRequest($"Chat messages should be less than {Limit.MAX_MESSAGE_LENGTH} characters.");
            }

            var chatGptChat = new List<ChatMessage>();
            foreach (var message in tutorChat)
            {
                if (string.IsNullOrWhiteSpace(message.Content))
                {
                    logger.LogWarning(
                        "Write chat request rejected for user {UserId}: message with role {Role} had empty content.",
                        userId,
                        message.Role);
                    operation.Complete("Result", "EmptyMessage");
                    return BadRequest("Message content cannot be empty.");
                }

                ChatMessage chatGptMessage = message.Role switch
                {
                    "user" => new UserChatMessage(message.Content),
                    "assistant" => new AssistantChatMessage(message.Content),
                    _ => throw LogUnsupportedRole(userId, message.Role)
                };
                chatGptChat.Add(chatGptMessage);
            }
            chatGptChat.Add(new SystemChatMessage(Prompts.SYSTEM_PROMPT));

            logger.LogDebug(
                "Write chat request for user {UserId} converted to {OpenAiMessageCount} OpenAI messages.",
                userId,
                chatGptChat.Count);

            TutorChatReply tutorChatReply;
            try
            {
                var response = await chatGptChatService.ChatAsync([.. chatGptChat], userId);
                var audioResponse = await chatGptAudioService.Speech(response, userId, token);
                tutorChatReply = new TutorChatReply(response, audioResponse);
            }
            catch (Exception ex)
            {
                operation.SetException(ex);
                operation.Abandon();
                throw;
            }

            logger.LogInformation("Write chat request completed for user {UserId};", userId);
            operation.Complete("Result", "Success");
            return Ok(tutorChatReply);
        }

        private Exception LogUnsupportedRole(string userId, string role)
        {
            logger.LogWarning(
                "Write chat request rejected for user {UserId}: unsupported message role {Role}.",
                userId,
                role);
            return new Exception($"{role} is not supported!");
        }
    }
}

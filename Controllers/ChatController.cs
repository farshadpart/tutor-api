using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenAI.Chat;
using Tutor.Api.Models.Constants;
using Tutor.Api.Models.Tutor.Api.Contracts.ChatServices;
using Tutor.Api.Services;

namespace Tutor.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    [EnableRateLimiting("Chat")]
    public class ChatController(ChatGptAudioService ChatGptAudioService, ChatGptChatService ChatGptChatService, ILogger<ChatController> Logger) : ControllerBase
    {
        [HttpPost("speak")]
        public async Task<IActionResult> Speak([FromForm] IFormFile voice)
        {
            string? userId = User.GetClaimValue(TutorClaimTypes.Id);
            if (userId is null)
            {
                Logger.LogError("The logged in user is not valid! User: {@user}", User);
                return BadRequest("The user is not valid!");
            }

            if(voice.Length >= Limit.MAX_VOICE_SIZE)
            {
                return BadRequest($"A voice message size should be less the {Limit.MAX_VOICE_SIZE / 3}MB.");
            }

            return Ok(await ChatGptAudioService.Transcribe(voice, userId));
        }

        [HttpPost("write")]
        public async Task<IActionResult> Write([FromBody] Message[] tutorChat)
        {
            string? userId = User.GetClaimValue(TutorClaimTypes.Id);
            if( userId is null)
            {
                Logger.LogError("The logged in user is not valid! User: {@user}", User);
                return BadRequest("The user is not valid!");
            }

            if (tutorChat == null || tutorChat.Length == 0)
            {
                return BadRequest("Chat messages cannot be null or empty.");
            }

            if(tutorChat.Any(x => !x.Role.Equals("system") && x.Content.Length >= Limit.MAX_MESSAGE_LENGTH))
            {
                return BadRequest($"Chat messages should be less than {Limit.MAX_MESSAGE_LENGTH} characters.");
            }

            var chatGptChat = new List<ChatMessage>();
            foreach (var message in tutorChat)
            {
                if (string.IsNullOrWhiteSpace(message.Content))
                {
                    return BadRequest("Message content cannot be empty.");
                }

                ChatMessage chatGptMessage = message.Role switch
                {
                    "user" => new UserChatMessage(message.Content),
                    "assistant" => new AssistantChatMessage(message.Content),
                    _ => throw new Exception($"{message.Role} is not supported!")
                };
                chatGptChat.Add(chatGptMessage);
            }
            chatGptChat.Add(new SystemChatMessage(Prompts.SYSTEM_PROMPT));

            return Ok(await ChatGptChatService.ChatAsync([.. chatGptChat], userId));
        }
    }
}

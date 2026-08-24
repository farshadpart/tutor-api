using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Tutor.Api.Filters;
using Tutor.Api.Models.Constants;
using Tutor.Api.Models.Tutor.Api.Contracts.ChatServices;
using Tutor.Api.Services;
using Tutor.Api.Validators;

namespace Tutor.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    [EnableRateLimiting("Chat")]
    [ServiceFilter(typeof(ControllerExecutionTimingFilter))]
    public class ChatController(
        TutorChatService tutorChatService,
        ILogger<ChatController> logger) : ControllerBase
    {
        [HttpPost("speak")]
        public async Task<IActionResult> Speak([FromForm] IFormFile voice)
        {
            var userId = User.GetClaimValue(TutorClaimTypes.Id);
            if (userId is null)
            {
                logger.LogError("The logged in user is not valid! User: {@user}", User);
                return BadRequest("The user is not valid!");
            }

            var validationResult = TutorChatValidator.Validate(voice);
            if (validationResult.IsFailed)
            {
                logger.LogWarning(
                    "Speak chat request rejected for user {UserId}: {@ValidationError}",
                    userId,
                    validationResult.Errors);
                return BadRequest(validationResult.Errors[0].Message);
            }

            logger.LogInformation(
                "Speak chat request received for user {UserId}; file name {FileName}, content type {ContentType}, size {SizeBytes} bytes.",
                userId,
                voice.FileName,
                voice.ContentType,
                voice.Length);

            var transcription = await tutorChatService.Transcribe(voice, userId);

            logger.LogInformation(
                "Speak chat request completed for user {UserId}; transcription length {TranscriptionLength}.",
                userId,
                transcription.Length);
            return Ok(transcription);
        }

        [HttpPost("write")]
        public async Task<IActionResult> Write([FromBody] Message[] tutorChat, CancellationToken token)
        {
            var userId = User.GetClaimValue(TutorClaimTypes.Id);
            if (userId is null)
            {
                logger.LogError("The logged in user is not valid! User: {@user}", User);
                return BadRequest("The user is not valid!");
            }

            var validationResult = TutorChatValidator.Validate(tutorChat);
            if (validationResult.IsFailed)
            {
                logger.LogWarning(
                    "Write chat request rejected for user {UserId}: {@ValidationError}",
                    userId,
                    validationResult.Errors);
                return BadRequest(validationResult.Errors[0].Message);
            }

            logger.LogInformation(
                "Write chat request received for user {UserId}; message count {MessageCount}, total content length {TotalContentLength}.",
                userId,
                tutorChat.Length,
                tutorChat.Sum(x => x.Content.Length));

            var tutorChatReply = await tutorChatService.ReplyAsync(tutorChat, userId, token);
            logger.LogInformation("Write chat request completed for user {UserId};", userId);
            return Ok(tutorChatReply);
        }
    }
}

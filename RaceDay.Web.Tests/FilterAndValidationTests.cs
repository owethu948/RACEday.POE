using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using RaceDay.Web.Controllers;
using RaceDay.Web.Filters;
using RaceDay.Web.Helpers;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Tests;

public class FilterAndValidationTests
{
    private static ActionExecutingContext ExecutingContext(HttpContext http) =>
        new(new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

    private static HttpContext SignedIn(string role) =>
        TestHttp.ContextWithSession(s =>
        {
            s.SetInt32(SessionKeys.UserId, 5);
            s.SetString(SessionKeys.Role, role);
        });

    // ------------------------------------------------------------------ role filter
    [Fact]
    public void RequireRole_Anonymous_IsSentToLogin()
    {
        var context = ExecutingContext(TestHttp.ContextWithSession());

        new RequireRoleAttribute(Roles.Organiser).OnActionExecuting(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("Account", redirect.ControllerName);
    }

    [Fact]
    public void RequireRole_WrongRole_IsSentHome()
    {
        var context = ExecutingContext(SignedIn(Roles.Participant));

        new RequireRoleAttribute(Roles.Organiser).OnActionExecuting(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public void RequireRole_CorrectRole_LetsTheRequestThrough()
    {
        var context = ExecutingContext(SignedIn(Roles.Organiser));

        new RequireRoleAttribute(Roles.Organiser).OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void RequireLogin_AnyRole_LetsTheRequestThrough()
    {
        var context = ExecutingContext(SignedIn(Roles.Participant));

        new RequireLoginAttribute().OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    // ------------------------------------------------------------------ results entry
    [Fact]
    public void Results_FinishedWithValidTimeAndPosition_IsAccepted()
    {
        var problem = OrganiserResultsController.Validate(
            new ResultFormModel { Status = "Finished", FinishTime = "1:45:32", Position = 214 }, out var time);

        Assert.Null(problem);
        Assert.Equal(new TimeSpan(1, 45, 32), time);
    }

    [Theory]
    [InlineData("45:32")]        // would silently mean 45 hours if parsed as a TimeSpan
    [InlineData("1:75:00")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void Results_FinishedWithBadTime_IsRejected(string? text)
    {
        var problem = OrganiserResultsController.Validate(
            new ResultFormModel { Status = "Finished", FinishTime = text, Position = 3 }, out var time);

        Assert.NotNull(problem);
        Assert.Null(time);
    }

    [Fact]
    public void Results_FinishedWithoutPosition_IsRejected()
    {
        var problem = OrganiserResultsController.Validate(
            new ResultFormModel { Status = "Finished", FinishTime = "1:00:00", Position = null }, out _);

        Assert.NotNull(problem);
    }

    [Theory]
    [InlineData("DNF")]
    [InlineData("DSQ")]
    public void Results_DidNotFinish_NeedsNoTimeOrPosition(string status)
    {
        var problem = OrganiserResultsController.Validate(new ResultFormModel { Status = status }, out var time);

        Assert.Null(problem);
        Assert.Null(time);
    }

    [Fact]
    public void Results_UnknownStatus_IsRejected() =>
        Assert.NotNull(OrganiserResultsController.Validate(new ResultFormModel { Status = "Winner" }, out _));

    // ------------------------------------------------------------------ image checks
    private static FormFile File(string name, long length) =>
        new(new MemoryStream(new byte[Math.Min(length, 16)]), 0, length, "file", name);

    [Fact]
    public void ImageUpload_NoFile_IsFine() => Assert.Null(ImageUploadValidator.Validate(null));

    [Fact]
    public void ImageUpload_SupportedImage_IsFine() => Assert.Null(ImageUploadValidator.Validate(File("race.JPG", 1024)));

    [Fact]
    public void ImageUpload_WrongExtension_IsRejected() => Assert.NotNull(ImageUploadValidator.Validate(File("virus.exe", 1024)));

    [Fact]
    public void ImageUpload_TooBig_IsRejected() =>
        Assert.NotNull(ImageUploadValidator.Validate(File("huge.png", ImageUploadValidator.MaxBytes + 1)));

    // ------------------------------------------------------------------ category rows on the create-event page
    [Fact]
    public void CategoryRow_Complete_IsAccepted() =>
        Assert.Null(OrganiserEventsController.CategoryRowProblem(new CategoryRowInput { Name = "21km", DistanceKm = 21.1m, MaxParticipants = 200, EntryFee = 250m }));

    [Fact]
    public void CategoryRow_FeeIsOptional() =>
        Assert.Null(OrganiserEventsController.CategoryRowProblem(new CategoryRowInput { Name = "5km", DistanceKm = 5m, MaxParticipants = 50 }));

    [Theory]
    [InlineData(null, 5.0, 50)]
    [InlineData("5km", null, 50)]
    [InlineData("5km", 0.0, 50)]
    [InlineData("5km", 5.0, null)]
    [InlineData("5km", 5.0, 0)]
    public void CategoryRow_Incomplete_IsRejected(string? name, double? km, int? max) =>
        Assert.NotNull(OrganiserEventsController.CategoryRowProblem(new CategoryRowInput { Name = name, DistanceKm = (decimal?)km, MaxParticipants = max }));

    [Fact]
    public void CategoryRow_BlankRowsAreIgnoredNotRejected() =>
        Assert.True(new CategoryRowInput().IsBlank);
}

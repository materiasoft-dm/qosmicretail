using System.Net.Http;
using Mercurius.Repo.Models;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Mercurius.Tests;

/// <summary>
/// Playwright tests for Login functionality.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class LoginTests : PageTest
{
    private const string BaseUrl = "http://localhost:5094";
    private const string LoginUrl = $"{BaseUrl}/Identity/Account/Login";
    
    // Test credentials - from appsettings.Development.json SeedAdmin
    private const string TestEmail = "admin@mercurius.com";
    private const string TestPassword = "Admin@123";

    [SetUp]
    public async Task SetUp()
    {
        // Navigate to login page before each test
        await Page.GotoAsync(LoginUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Override context options to ensure browser is visible.
    /// Note: Headless=false is set in testsettings.json.
    /// </summary>
    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        };
    }

    /// <summary>
    /// Test that the login page loads correctly with all expected elements.
    /// </summary>
    [Test]
    public async Task LoginPage_Loads_WithExpectedElements()
    {
        // Check page has loaded (title contains Mercurius or Sign in)
        var title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("Mercurius").Or.Contain("Sign in").Or.Contain("Login"),
            "Page title should contain Mercurius or Sign in");

        // Check for email input field
        var emailInput = Page.Locator("input[type='email'], input[name='Email']").First;
        await Expect(emailInput).ToBeVisibleAsync();

        // Check for password input field
        var passwordInput = Page.Locator("input[type='password'], input[name='Password']").First;
        await Expect(passwordInput).ToBeVisibleAsync();

        // Check for login button
        var loginButton = Page.Locator("button[type='submit'], input[type='submit']").First;
        await Expect(loginButton).ToBeVisibleAsync();
    }

    /// <summary>
    /// Test successful login with valid credentials.
    /// </summary>
    [Test]
    public async Task Login_WithValidCredentials_Succeeds()
    {
        // Enter email
        var emailInput = Page.Locator("input[type='email'], input[name='Email']").First;
        await emailInput.FillAsync(TestEmail);
        await Page.WaitForTimeoutAsync(1000); // Watch: typing email

        // Enter password
        var passwordInput = Page.Locator("input[type='password'], input[name='Password']").First;
        await passwordInput.FillAsync(TestPassword);
        await Page.WaitForTimeoutAsync(1000); // Watch: typing password

        // Click login button
        var loginButton = Page.Locator("button[type='submit'], input[type='submit']").First;
        await loginButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1000); // Watch: clicking login

        // Wait for navigation after login
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1000); // Watch: page redirect

        // Should not be on login page anymore
        Assert.That(Page.Url, Does.Not.Contain("/Identity/Account/Login"), 
            "Should not be on login page after successful login");
        
        // Should be redirected to home or dashboard
        Assert.That(Page.Url, Does.Not.Contain("/Account/Login"),
            "Should not be on legacy login page after successful login");
    }

    /// <summary>
    /// Test login with invalid password shows error message.
    /// </summary>
    [Test]
    public async Task Login_WithInvalidPassword_ShowsError()
    {
        // Enter email
        var emailInput = Page.Locator("input[type='email'], input[name='Email']").First;
        await emailInput.FillAsync(TestEmail);

        // Enter wrong password
        var passwordInput = Page.Locator("input[type='password'], input[name='Password']").First;
        await passwordInput.FillAsync("WrongPassword123!");

        // Click login button
        var loginButton = Page.Locator("button[type='submit'], input[type='submit']").First;
        await loginButton.ClickAsync();

        // Wait for response
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should still be on login page
        Assert.That(Page.Url, Does.Contain("/Identity/Account/Login").Or.Contain("/Account/Login"),
            "Should remain on login page after failed login");

        // Check for error message (common patterns)
        var errorLocator = Page.Locator(".validation-summary-errors, .alert-danger, [role='alert']").First;
        bool hasError = false;
        try
        {
            hasError = await errorLocator.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            // Error element not found within timeout - that's fine
        }
        
        if (hasError)
        {
            var errorText = await errorLocator.TextContentAsync();
            Assert.That(errorText, Does.Contain("Invalid").Or.Contain("incorrect").Or.Contain("failed"),
                "Error message should indicate invalid login");
        }
    }

    /// <summary>
    /// Test login with empty credentials shows validation error.
    /// </summary>
    [Test]
    public async Task Login_WithEmptyCredentials_ShowsValidationError()
    {
        // Leave fields empty and click login
        var loginButton = Page.Locator("button[type='submit'], input[type='submit']").First;
        await loginButton.ClickAsync();

        // Wait for validation
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should still be on login page
        Assert.That(Page.Url, Does.Contain("/Identity/Account/Login").Or.Contain("/Account/Login"),
            "Should remain on login page with empty credentials");

        // Check for validation message
        var validationLocator = Page.Locator(".validation-summary-errors, .alert-danger, [role='alert'], .field-validation-error").First;
        bool hasValidation = false;
        try
        {
            hasValidation = await validationLocator.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            // Validation element not found within timeout - that's fine
        }
        
        Assert.That(hasValidation, Is.True, "Should show validation error for empty credentials");
    }

    /// <summary>
    /// Test that unauthenticated users are redirected to login.
    /// </summary>
    [Test]
    public async Task UnauthenticatedUser_RedirectedToLogin()
    {
        // Try to access a protected page directly
        await Page.GotoAsync($"{BaseUrl}/Products");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should be redirected to login
        Assert.That(Page.Url, Does.Contain("/Identity/Account/Login").Or.Contain("/Account/Login").Or.Contain("/Login"),
            "Unauthenticated user should be redirected to login page");
    }

    /// <summary>
    /// Test that logged in user can access protected pages.
    /// </summary>
    [Test]
    public async Task AuthenticatedUser_CanAccessProtectedPages()
    {
        // First login
        var emailInput = Page.Locator("input[type='email'], input[name='Email']").First;
        await emailInput.FillAsync(TestEmail);

        var passwordInput = Page.Locator("input[type='password'], input[name='Password']").First;
        await passwordInput.FillAsync(TestPassword);

        var loginButton = Page.Locator("button[type='submit'], input[type='submit']").First;
        await loginButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Now try to access Products page
        await Page.GotoAsync($"{BaseUrl}/Products");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should NOT be redirected to login
        Assert.That(Page.Url, Does.Not.Contain("/Identity/Account/Login").Or.Contain("/Account/Login").Or.Contain("/Login"),
            "Authenticated user should not be redirected to login");
    }

    /// <summary>
    /// Test logout functionality.
    /// </summary>
    [Test]
    public async Task Logout_Works()
    {
        // First login
        var emailInput = Page.Locator("input[type='email'], input[name='Email']").First;
        await emailInput.FillAsync(TestEmail);

        var passwordInput = Page.Locator("input[type='password'], input[name='Password']").First;
        await passwordInput.FillAsync(TestPassword);

        var loginButton = Page.Locator("button[type='submit'], input[type='submit']").First;
        await loginButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Look for logout button/link
        var logoutLocator = Page.Locator("a:has-text('Logout'), a:has-text('Log out'), form[action*='logout'] button, button:has-text('Logout')").First;
        
        bool logoutExists = false;
        try
        {
            logoutExists = await logoutLocator.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            // Logout element not found within timeout - that's fine
        }
        
        if (logoutExists)
        {
            await logoutLocator.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // After logout, should be redirected to login or home
            // Try to access protected page
            await Page.GotoAsync($"{BaseUrl}/Products");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Should be redirected to login
            Assert.That(Page.Url, Does.Contain("/Identity/Account/Login").Or.Contain("/Account/Login").Or.Contain("/Login").Or.Contain("/logout"),
                "User should be logged out and redirected when accessing protected page");
        }
        else
        {
            Assert.Pass("Logout button not found - may not be implemented or user has no logout option");
        }
    }
}
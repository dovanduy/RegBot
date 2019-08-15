﻿using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Common.Service;
using Common.Service.Enums;
using Common.Service.Interfaces;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;

namespace Yandex.Bot
{
    public class YandexRegistration : IBot
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(YandexRegistration));
        private readonly IAccountData _data;
        private readonly ISmsService _smsService;
        private string _requestId;
        private readonly string _chromiumPath;

        public YandexRegistration(IAccountData data, ISmsService smsService, string chromiumPath)
        {
            _data = data;
            _data.Domain = "yandex.ru";
            _smsService = smsService;
            if (string.IsNullOrEmpty(chromiumPath)) chromiumPath = Environment.CurrentDirectory;
            chromiumPath = Path.Combine(chromiumPath, ".local-chromium\\Win64-662092\\chrome-win\\chrome.exe");
            _chromiumPath = chromiumPath;
        }

        public async Task<IAccountData> Registration(CountryCode countryCode = CountryCode.RU, bool headless = true)
        {
            try
            {

                var options = new LaunchOptions
                {
                    Headless = headless,
                    ExecutablePath = _chromiumPath,
                    //SlowMo = 10,

                };

                //options.Args = new[]
                //{
                //    "--proxy-server=socks4://36.67.184.157:54555"//, "--proxy-auth: userx:passx", "--proxy-type: 'meh'"
                //};
                //https://blog.apify.com/how-to-make-headless-chrome-and-puppeteer-use-a-proxy-server-with-authentication-249a21a79212
                //https://toster.ru/q/562104

                // windows7 websocket https://github.com/PingmanTools/System.Net.WebSockets.Client.Managed
                if (Environment.OSVersion.VersionString.Contains("NT 6.1")) { options.WebSocketFactory = WebSocketFactory; }
                _data.PhoneCountryCode = Enum.GetName(typeof(CountryCode), countryCode)?.ToUpper();
                Log.Info($"Registration data: {JsonConvert.SerializeObject(_data)}");
                var phoneNumberRequest = await _smsService.GetPhoneNumber(countryCode, ServiceCode.Yandex);
                //var phoneNumberRequest = new PhoneNumberRequest{Id="1", Phone = "79852985779"};
                if (phoneNumberRequest == null)
                {
                    _data.ErrMsg = BotMessages.NoPhoneNumberMessage;
                    return _data;
                }
                Log.Info($"phoneNumberRequest: {JsonConvert.SerializeObject(phoneNumberRequest)}");
                _requestId = phoneNumberRequest.Id;
                _data.Phone = phoneNumberRequest.Phone.Trim();
                if (!_data.Phone.StartsWith("+")) _data.Phone = $"+{_data.Phone}";

                using (var browser = await Puppeteer.LaunchAsync(options))
                using (var page = await browser.NewPageAsync())
                {
                    await FillRegistrationData(page);
                    //await page.ClickAsync("button[type='submit']");
                    await page.ClickAsync("div.registration__send-code span");
                    await page.WaitForTimeoutAsync(2000);
                    const string smsSendSelector = "div.reg-field__popup span.registration__pseudo-link";
                    ElementHandle smsCodeInput;
                    var phoneCodeLabelTextToken = await page.EvaluateExpressionAsync("document.querySelector('label[for=phoneCode').innerText");
                    var isSms = false;
                    var isVoice = false;
                    if (phoneCodeLabelTextToken != null)
                    {
                        var phoneCodeLabelText = phoneCodeLabelTextToken.ToString();
                        if (phoneCodeLabelText.Contains("смс")) isSms = true;
                        if (phoneCodeLabelText.Contains("голос")) isVoice = true;
                    }
                    // ошибка превышен лимит sms
                    var err = await page.QuerySelectorAsync("div.error-message");
                    //if (smsSendExists == null && smsCodeInput == null && err ==null) await page.WaitForTimeoutAsync(20000);
                    if (isVoice)
                    {
                        await page.WaitForTimeoutAsync(35000);
                        var jsAltAction = $@"Array.from(document.querySelectorAll('{smsSendSelector}')).map(a => a.innerText);";
                        var linkList = await page.EvaluateExpressionAsync<string[]>(jsAltAction);
                        var smsLink = linkList.FirstOrDefault(z => z.Contains("sms"));
                        if (!string.IsNullOrEmpty(smsLink))
                        {
                            var idx = Array.IndexOf(linkList, smsLink);
                            var altMailElements = await page.QuerySelectorAllAsync(smsSendSelector);
                            if (altMailElements != null && altMailElements.Length > idx)
                            {
                                await altMailElements[idx].ClickAsync();
                                var phoneNumberValidation = await _smsService.GetSmsValidation(_requestId);
                                Log.Info($"phoneNumberValidation: {JsonConvert.SerializeObject(phoneNumberValidation)}");
                                if (phoneNumberValidation != null)
                                {
                                    await _smsService.SetSmsValidationSuccess(_requestId);
                                    smsCodeInput = await page.QuerySelectorAsync("input#phoneCode");
                                    if (smsCodeInput != null)
                                    {
                                        await page.TypeAsync("input#phoneCode", phoneNumberValidation.Code);
                                        await page.WaitForTimeoutAsync(5000);
                                        await page.ClickAsync("button[type='submit']");
                                        await page.WaitForTimeoutAsync(5000);
                                        _data.Success = true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            smsCodeInput = await page.QuerySelectorAsync("input#phoneCode");
                            if (smsCodeInput != null)
                            {
                                var phoneNumberValidation = await _smsService.GetSmsValidation(_requestId);
                                Log.Info($"phoneNumberValidation: {JsonConvert.SerializeObject(phoneNumberValidation)}");
                                await page.TypeAsync("input#phoneCode", phoneNumberValidation.Code);
                                await _smsService.SetSmsValidationSuccess(_requestId);
                                await page.WaitForTimeoutAsync(5000);
                                await page.ClickAsync("button[type='submit']");
                                await page.WaitForTimeoutAsync(5000);
                                _data.Success = true;
                            }
                        }
                    }

                    if (isSms)
                    {
                        smsCodeInput = await page.QuerySelectorAsync("input#phoneCode");
                        if (smsCodeInput != null)
                        {
                            var phoneNumberValidation = await _smsService.GetSmsValidation(_requestId);
                            Log.Info($"phoneNumberValidation: {JsonConvert.SerializeObject(phoneNumberValidation)}");
                            await page.TypeAsync("input#phoneCode", phoneNumberValidation.Code);
                            await _smsService.SetSmsValidationSuccess(_requestId);
                            await page.WaitForTimeoutAsync(5000);
                            await page.ClickAsync("button[type='submit']");
                            var selEula = "div.t-eula-accept button";
                            var elEula = await page.QuerySelectorAsync(selEula);
                            var eulaButtonVisible = elEula != null && await elEula.IsIntersectingViewportAsync();
                            if (eulaButtonVisible) await elEula.ClickAsync();
                            await page.WaitForTimeoutAsync(5000);
                            _data.Success = true;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception);
                _data.ErrMsg = exception.Message;
            }

            return _data;
        }

        private async Task FillRegistrationData(Page page)
        {
            await page.GoToAsync("https://passport.yandex.ru/registration/mail?from=mail&origin=home_desktop_ru&retpath=https%3A%2F%2Fmail.yandex.ru%2F");

            #region Name

            await page.TypeAsync("input[name=firstname]", _data.Firstname);
            await page.TypeAsync("input[name=lastname]", _data.Lastname);

            #endregion

            #region Login

            if (string.IsNullOrEmpty(_data.AccountName))
            {
                _data.AccountName = $"{_data.Firstname.ToLower()}.{_data.Lastname.ToLower()}";
            }

            await page.TypeAsync("input[name=login]", _data.AccountName);


            const string selAltMail = "li.registration__pseudo-link label";
            await page.WaitForTimeoutAsync(300);
            var altMailExists = await page.QuerySelectorAsync(selAltMail);
            //var altMailExists = await page.WaitForSelectorAsync(selAltMail, new WaitForSelectorOptions { Timeout = 300 });
            if (altMailExists != null)
            {
                var selAltMailList = $"{selAltMail}";
                var jsAltMailList = $@"Array.from(document.querySelectorAll('{selAltMailList}')).map(a => a.innerText);";
                var altMailList = await page.EvaluateExpressionAsync<string[]>(jsAltMailList);
                var altEmail = altMailList.FirstOrDefault();
                if (string.IsNullOrEmpty(altEmail)) altEmail = altMailList[0];
                _data.AccountName = altEmail.Split('@')[0];
                var idx = Array.IndexOf(altMailList, altEmail);
                var altMailElements = await page.QuerySelectorAllAsync(selAltMailList);
                if (altMailElements != null && altMailElements.Length > idx) await altMailElements[idx].ClickAsync();
            }

            #endregion

            #region Password

            await page.TypeAsync("input[name=password]", _data.Password);
            await page.TypeAsync("input[name=password_confirm]", _data.Password);

            #endregion

            #region Phone

            const string selPhone = "input[name=phone]";
            await page.ClickAsync(selPhone);
            await page.EvaluateFunctionAsync("function() {" + $"document.querySelector('{selPhone}').value = ''" + "}");
            await page.TypeAsync(selPhone, _data.Phone);
            //await page.ClickAsync("div.registration__send-code button");

            #endregion

            #region not use yandex wallet

            const string selWallet = "div.form__eula_money span";
            var elWallet = await page.QuerySelectorAsync(selWallet);
            if (elWallet != null) await elWallet.ClickAsync();

            #endregion



            //
        }

        private async Task<WebSocket> WebSocketFactory(Uri url, IConnectionOptions options,
            CancellationToken cancellationToken)
        {
            var ws = new System.Net.WebSockets.Managed.ClientWebSocket();
            await ws.ConnectAsync(url, (CancellationToken)cancellationToken);
            return ws;
        }
    }
}

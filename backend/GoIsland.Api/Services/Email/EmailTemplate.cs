using System.Net;

namespace GoIsland.Api.Services.Email;

public sealed record EmailContent(string Subject, string HtmlBody, string TextBody);

public static class EmailTemplate
{
    private const string BrandName = "GoIsland";
    private const string PrimaryColor = "#176F9F";
    private const string DarkColor = "#123B57";
    private const string GoldColor = "#F4B942";

    public static string Render(
        string previewText,
        string eyebrow,
        string title,
        string greeting,
        string bodyHtml,
        string? actionLabel = null,
        string? actionUrl = null,
        string? note = null)
    {
        var safePreview = WebUtility.HtmlEncode(previewText);
        var safeEyebrow = WebUtility.HtmlEncode(eyebrow);
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeGreeting = WebUtility.HtmlEncode(greeting);
        var safeNote = string.IsNullOrWhiteSpace(note) ? null : WebUtility.HtmlEncode(note);
        var action = string.IsNullOrWhiteSpace(actionLabel) || string.IsNullOrWhiteSpace(actionUrl)
            ? string.Empty
            : $"""
               <tr>
                 <td style="padding:8px 40px 32px 40px;">
                   <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                     <tr>
                       <td style="border-radius:10px;background:{PrimaryColor};">
                         <a href="{WebUtility.HtmlEncode(actionUrl)}"
                            style="display:inline-block;padding:14px 22px;color:#ffffff;text-decoration:none;font-family:Arial,sans-serif;font-size:15px;font-weight:700;">
                           {WebUtility.HtmlEncode(actionLabel)}
                         </a>
                       </td>
                     </tr>
                   </table>
                 </td>
               </tr>
               """;
        var noteBlock = safeNote is null
            ? string.Empty
            : $"""
               <tr>
                 <td style="padding:0 40px 32px 40px;">
                   <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0"
                          style="border-left:4px solid {GoldColor};background:#F7FAFC;border-radius:8px;">
                     <tr>
                       <td style="padding:14px 16px;color:#536B7A;font-family:Arial,sans-serif;font-size:13px;line-height:1.55;">
                         {safeNote}
                       </td>
                     </tr>
                   </table>
                 </td>
               </tr>
               """;

        return $$"""
            <!doctype html>
            <html lang="es">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <meta name="color-scheme" content="light">
              <title>{{safeTitle}}</title>
              <style>
                @media only screen and (max-width:620px) {
                  .email-shell { width:100% !important; border-radius:0 !important; }
                  .email-padding { padding-left:24px !important; padding-right:24px !important; }
                  .email-title { font-size:26px !important; }
                }
              </style>
            </head>
            <body style="margin:0;padding:0;background:#EDF4F7;">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">
                {{safePreview}}
              </div>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0"
                     style="width:100%;background:#EDF4F7;">
                <tr>
                  <td align="center" style="padding:32px 12px;">
                    <table role="presentation" width="600" cellspacing="0" cellpadding="0" border="0"
                           class="email-shell"
                           style="width:600px;max-width:100%;overflow:hidden;border-radius:18px;background:#ffffff;box-shadow:0 12px 32px rgba(18,59,87,.12);">
                      <tr>
                        <td style="padding:25px 40px;background:{{DarkColor}};">
                          <span style="color:#ffffff;font-family:Georgia,serif;font-size:25px;font-weight:700;letter-spacing:-.4px;">
                            Go<span style="color:{{GoldColor}};">Island</span>
                          </span>
                        </td>
                      </tr>
                      <tr>
                        <td class="email-padding" style="padding:38px 40px 14px 40px;">
                          <div style="margin-bottom:10px;color:{{PrimaryColor}};font-family:Arial,sans-serif;font-size:12px;font-weight:700;letter-spacing:1.4px;text-transform:uppercase;">
                            {{safeEyebrow}}
                          </div>
                          <h1 class="email-title" style="margin:0;color:{{DarkColor}};font-family:Georgia,serif;font-size:32px;line-height:1.2;">
                            {{safeTitle}}
                          </h1>
                        </td>
                      </tr>
                      <tr>
                        <td class="email-padding" style="padding:12px 40px 24px 40px;color:#425D6C;font-family:Arial,sans-serif;font-size:16px;line-height:1.7;">
                          <p style="margin:0 0 14px 0;color:{{DarkColor}};font-weight:700;">{{safeGreeting}}</p>
                          {{bodyHtml}}
                        </td>
                      </tr>
                      {{action}}
                      {{noteBlock}}
                      <tr>
                        <td class="email-padding" style="padding:22px 40px;border-top:1px solid #DDE8ED;background:#F8FBFC;color:#708591;font-family:Arial,sans-serif;font-size:12px;line-height:1.6;">
                          <strong style="color:{{DarkColor}};">{{BrandName}}</strong><br>
                          Experiencias auténticas en República Dominicana.<br>
                          Este es un mensaje automático; no es necesario responder.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MRochon.Extensions
{
    public class InvitationOptions
    {
        public string? TenantName { get; set; }
        public string? ClientId { get; set; }
        public string? Issuer { get; set; }
        public string? Audience { get; set; }
        public string? Policy { get; set; }
        public string? SigningKey { get; set; }
        public int ValidityMinutes { get; set; }
        public string? RedirectUri { get; set; }
        public IDictionary<string,string>? DomainMappings { get; set; }  // maps user email domains to domain names known to IEF; expected user email domains must be all lower case; IEF must be same as inn xml
    }
    public class InvitationService
    {
        private readonly ILogger<InvitationService> _logger;
        private readonly IOptions<InvitationOptions> _options;
        public InvitationService(ILogger<InvitationService> logger,
            IOptions<InvitationOptions> tokenOptions)
        {
            _logger = logger;
            _options = tokenOptions;
        }

        public string Invite(string email, IDictionary<string, string>? additionalClaims = null, IDictionary<string,string>? optionalParams = null)
        {
            IList<System.Security.Claims.Claim> claims = new List<System.Security.Claims.Claim>();
            claims.Add(new System.Security.Claims.Claim("email", email));
            if (additionalClaims != null)
            {
                foreach (var c in additionalClaims)
                {
                    claims.Add(new System.Security.Claims.Claim(c.Key, c.Value));
                }
            }
            var replyUrl = System.Web.HttpUtility.UrlEncode(_options.Value.RedirectUri);
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Value.SigningKey));
            var cred = new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256Signature);
            var token = new JwtSecurityToken(
                issuer: _options.Value.Issuer,
                audience: _options.Value.Audience,
                claims,
                DateTime.Now,
                DateTime.Now.AddMinutes(_options.Value.ValidityMinutes),
                cred);
            var jwtHandler = new JwtSecurityTokenHandler();
            var jwt = jwtHandler.WriteToken(token); 
            var url = $"https://{_options.Value.TenantName}.b2clogin.com/{_options.Value.TenantName}.onmicrosoft.com/{_options.Value.Policy}/oauth2/v2.0/authorize?client_id={_options.Value.ClientId}&login_hint={email}&response_mode=form_post&nonce=defaultNonce&redirect_uri={replyUrl}&scope=openid&response_type=code&prompt=login&client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer&client_assertion={jwt}";
            if(optionalParams != null)
            {
                foreach(var p in optionalParams)
                {
                    url += $"&{p.Key}={p.Value}";
                }
            }
            if (_options.Value.DomainMappings != null)
            {
                var userDomain = email.Split('@')[1];
                if (_options.Value.DomainMappings.ContainsKey(userDomain.ToLower()))
                {
                    url += $"domain_hint={_options.Value.DomainMappings[userDomain.ToLower()]}";
                }
            }
            return url;
        }
    }
}

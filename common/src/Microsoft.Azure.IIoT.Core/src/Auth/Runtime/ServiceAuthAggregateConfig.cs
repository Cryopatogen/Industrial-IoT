// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Azure.IIoT.Auth.Runtime {
    using Microsoft.Azure.IIoT.Auth.Server;
    using Microsoft.Azure.IIoT.Utils;
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Service auth configuration
    /// </summary>
    public class ServiceAuthAggregateConfig : ConfigBase, IServerAuthConfig {

        /// <summary>
        /// Auth configuration
        /// </summary>
        private const string kAuth_RequiredKey = "Auth:Required";

        /// <inheritdoc/>
        public bool AllowAnonymousAccess => !GetBoolOrDefault(kAuth_RequiredKey,
            () => GetBoolOrDefault(PcsVariable.PCS_AUTH_RQUIRED,
                () => true));

        /// <inheritdoc/>
        public IEnumerable<IOAuthServerConfig> JwtBearerSchemes { get; }

        /// <summary>
        /// Configuration constructor
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="schemes"></param>
        public ServiceAuthAggregateConfig(IConfiguration configuration, IEnumerable<IOAuthServerConfig> schemes) :
            base(configuration) {
            JwtBearerSchemes = schemes?.Where(s => !string.IsNullOrEmpty(s.Audience)).ToList()
                ?? throw new ArgumentNullException(nameof(schemes));
        }
    }
}

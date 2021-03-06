﻿using Kentor.AuthServices.Metadata;
using Kentor.AuthServices.Saml2P;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IdentityModel.Configuration;
using System.IdentityModel.Metadata;
using System.IdentityModel.Services.Configuration;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Kentor.AuthServices.Configuration
{
    /// <summary>
    /// Options for the service provider's behaviour; i.e. everything except
    /// the idp and federation list.
    /// </summary>
    public class SPOptions
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public SPOptions()
        {
            systemIdentityModelIdentityConfiguration = new IdentityConfiguration(false);
            MetadataCacheDuration = new TimeSpan(1, 0, 0);
            Compatibility = new Compatibility();
            SigningAlgorithm = XmlHelpers.GetDefaltSigningAlgorithmName();
        }

        /// <summary>
        /// Construct the options from the given configuration section
        /// </summary>
        /// <param name="configSection"></param>
        public SPOptions(KentorAuthServicesSection configSection)
        {
            if (configSection == null)
            {
                throw new ArgumentNullException(nameof(configSection));
            }
            systemIdentityModelIdentityConfiguration = new IdentityConfiguration(true);

            ReturnUrl = configSection.ReturnUrl;
            MetadataCacheDuration = configSection.Metadata.CacheDuration;
            MetadataValidDuration = configSection.Metadata.ValidUntil;
            WantAssertionsSigned = configSection.Metadata.WantAssertionsSigned;
            ValidateCertificates = configSection.ValidateCertificates;
            DiscoveryServiceUrl = configSection.DiscoveryServiceUrl;
            EntityId = configSection.EntityId;
            ModulePath = configSection.ModulePath;
            PublicOrigin = configSection.PublicOrigin;
            Organization = configSection.Organization;
            SigningAlgorithm = XmlHelpers.GetFullSigningAlgorithmName(configSection.SigningAlgorithm);
            AuthenticateRequestSigningBehavior = configSection.AuthenticateRequestSigningBehavior;
            NameIdPolicy = new Saml2NameIdPolicy(
                configSection.NameIdPolicyElement.AllowCreate, configSection.NameIdPolicyElement.Format);
            RequestedAuthnContext = new Saml2RequestedAuthnContext(configSection.RequestedAuthnContext);
            Compatibility = new Compatibility(configSection.Compatibility);

            configSection.ServiceCertificates.RegisterServiceCertificates(this);

            foreach (var acs in configSection.AttributeConsumingServices)
            {
                AttributeConsumingServices.Add(acs);
            }

            foreach (var contact in configSection.Contacts)
            {
                Contacts.Add(contact);
            }
        }

        /// <summary>
        /// Return Uri to redirect the client to, if no return uri was specified
        /// when initiating the signin sequence.
        /// </summary>
        public Uri ReturnUrl { get; set; }

        /// <summary>
        /// Recommendation of cache refresh interval to those who reads our
        /// metadata.
        /// </summary>
        public TimeSpan MetadataCacheDuration { get; set; }

        /// <summary>
        /// Maximum validity duration after fetch for those who reads our
        /// metadata. Exposed as an absolute validUntil time in the metadata.
        /// If set to null, no validUntil is exposed in metadata.
        /// </summary>
        public TimeSpan? MetadataValidDuration { get; set; }

        volatile private Saml2PSecurityTokenHandler saml2PSecurityTokenHandler;

        /// <summary>
        /// The security token handler used to process incoming assertions for this SP.
        /// The default value is to lazy create one using the current EntityId.
        /// </summary>
        public Saml2PSecurityTokenHandler Saml2PSecurityTokenHandler
        {
            get
            {
                // Capture in a local variable to prevent race conditions. Reads and writes
                // of references are atomic so there is no need for a lock.
                var value = saml2PSecurityTokenHandler;
                if(value == null)
                {
                    // Set the saved value, but don't trust it - still use a local var for the return.
                    saml2PSecurityTokenHandler = value = new Saml2PSecurityTokenHandler(this);
                }

                return value;
            }
            set
            {
                saml2PSecurityTokenHandler = value; 
            }
        }

        /// <summary>
        /// Url to discovery service to use if no idp is specified in the sign in call.
        /// </summary>
        public Uri DiscoveryServiceUrl { get; set; }

        private EntityId entityId;

        /// <summary>
        /// EntityId - The identity of the ServiceProvider to use when sending requests to Idp
        /// and presenting the SP in metadata.
        /// </summary>
        public EntityId EntityId
        {
            get
            {
                return entityId;
            }
            set
            {
                if(saml2PSecurityTokenHandler != null)
                {
                    throw new InvalidOperationException("Can't change entity id when a token handler has been instantiated.");
                }
                entityId = value;
            }
        }

        private string modulePath = "/AuthServices";

        /// <summary>
        /// Application root relative path for AuthServices endpoints. The
        /// default is "/AuthServices".
        /// </summary>
        public string ModulePath
        {
            get
            {
                return modulePath;
            }
            set
            {
                if(value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                value = value.TrimEnd('/');

                if (!value.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                {
                    value = "/" + value;
                }

                modulePath = value;
            }
        }

        /// <summary>
        /// By default, the service provider uses the host, protocol, port and
        /// application root path from the HTTP request when creating links. 
        /// This might not be accurate in reverse proxy or load-balancing
        /// situations. You can override the origin used for link generation
        /// for the entire application using this property. To override per request,
        /// implement a <code>GetPublicOrigin</code> Notification function.
        /// </summary>
        public Uri PublicOrigin { get; set; }

        /// <summary>
        /// Metadata describing the organization responsible for the entity.
        /// </summary>
        public Organization Organization { get; set; }

        /// <summary>
        /// NameId Policy.
        /// </summary>
        public Saml2NameIdPolicy NameIdPolicy { get; set; }

        /// <summary>
        /// RequestedAuthnContext
        /// </summary>
        public Saml2RequestedAuthnContext RequestedAuthnContext { get; set; }

        readonly ICollection<ContactPerson> contacts = new List<ContactPerson>();

        /// <summary>
        /// Collection of contacts for the SAML2 entity.
        /// </summary>
        public ICollection<ContactPerson> Contacts
        {
            get
            {
                return contacts;
            }
        }

        readonly ICollection<AttributeConsumingService> attributeConsumingServices = new List<AttributeConsumingService>();

        /// <summary>
        /// Collection of attribute consuming services for the service provider.
        /// </summary>
        public ICollection<AttributeConsumingService> AttributeConsumingServices
        {
            get
            {
                return attributeConsumingServices;
            }
        }

        private IdentityConfiguration systemIdentityModelIdentityConfiguration;

        /// <summary>
        /// The System.IdentityModel configuration to use.
        /// </summary>
        public IdentityConfiguration SystemIdentityModelIdentityConfiguration
        {
            get
            {
                return systemIdentityModelIdentityConfiguration;
            }
        }

        readonly ServiceCertificateCollection serviceCertificates = new ServiceCertificateCollection();

        /// <summary>
        /// Certificates used by the service provider for signing or decryption.
        /// </summary>
        public ServiceCertificateCollection ServiceCertificates
        {
            get
            {
                return serviceCertificates;
            }
        }

        /// <summary>
        /// Certificates valid for use in decryption
        /// </summary>
        public ReadOnlyCollection<X509Certificate2> DecryptionServiceCertificates
        {
            get
            {
                var decryptionCertificates = ServiceCertificates
                    .Where(c => c.Use == CertificateUse.Encryption || c.Use == CertificateUse.Both)
                    .Select(c => c.Certificate);

                return decryptionCertificates.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Certificate for use in signing outbound requests
        /// </summary>
        public X509Certificate2 SigningServiceCertificate
        {
            get
            {
                var signingCertificates = ServiceCertificates
                    .Where(c => c.Status == CertificateStatus.Current)
                    .Where(c => c.Use == CertificateUse.Signing || c.Use == CertificateUse.Both)
                    .Select(c => c.Certificate);

                return signingCertificates.FirstOrDefault();
            }
        }

        /// <summary>
        /// Certificates to be published in metadata
        /// </summary>
        public ReadOnlyCollection<ServiceCertificate> MetadataCertificates
        {
            get
            {
                var futureEncryptionCertExists = publishableServiceCertificates
                    .Any(c => c.Status == CertificateStatus.Future && (c.Use == CertificateUse.Encryption || c.Use == CertificateUse.Both));

                var metaDataCertificates = publishableServiceCertificates
                    .Where(
                        // Signing & "Both" certs always get published because we want Idp's to be aware of upcoming keys
                        c => c.Status == CertificateStatus.Future || c.Use != CertificateUse.Encryption
                        // But current Encryption cert stops getting published immediately when a Future one is added
                        // (of course we still decrypt with the current cert, but that's a different part of the code)
                        || (c.Status == CertificateStatus.Current && c.Use == CertificateUse.Encryption && !futureEncryptionCertExists)
                        || c.MetadataPublishOverride != MetadataPublishOverrideType.None
                    )
                    .Select(c => new ServiceCertificate
                    {
                        Use = c.Use,
                        Status = c.Status,
                        MetadataPublishOverride = c.MetadataPublishOverride,
                        Certificate = c.Certificate
                    }).ToList();

                var futureBothCertExists = metaDataCertificates
                    .Any(c => c.Status == CertificateStatus.Future && c.Use == CertificateUse.Both);

                foreach(var cert in metaDataCertificates)
                {
                    // Just like we stop publishing Encryption cert immediately when a Future one is added,
                    // in the case of a "Both" cert we should switch the current use to Signing so that Idp's stop sending
                    // us certs encrypted with the old key
                    if (cert.Use == CertificateUse.Both && cert.Status == CertificateStatus.Current && futureBothCertExists)
                    {
                        cert.Use = CertificateUse.Signing;
                    }

                    if (cert.MetadataPublishOverride == MetadataPublishOverrideType.PublishEncryption)
                    {
                        cert.Use = CertificateUse.Encryption;
                    }
                    if (cert.MetadataPublishOverride == MetadataPublishOverrideType.PublishSigning)
                    {
                        cert.Use = CertificateUse.Signing;
                    }
                    if (cert.MetadataPublishOverride == MetadataPublishOverrideType.PublishUnspecified)
                    {
                        cert.Use = CertificateUse.Both;
                    }
                }

                return metaDataCertificates.AsReadOnly();
            }
        }

        private IEnumerable<ServiceCertificate> publishableServiceCertificates
        {
            get
            {
                return ServiceCertificates
                    .Where(c => c.MetadataPublishOverride != MetadataPublishOverrideType.DoNotPublish);
            }
        }

        /// <summary>
        /// Signing behaviour for AuthnRequests.
        /// </summary>
        public SigningBehavior AuthenticateRequestSigningBehavior { get; set; }

        /// <summary>
        /// Signing algorithm for metadata and outbound messages. Can be 
        /// overriden for each <see cref="IdentityProvider"/>.
        /// </summary>
        public string SigningAlgorithm { get; set; }
        
        /// <summary>
        /// Metadata flag that we want assertions to be signed.
        /// </summary>
        public bool WantAssertionsSigned { get; set; }

        /// <summary>
        /// Validate certificates when validating signatures? Normally not a
        /// good idea as SAML2 deployments typically exchange certificates
        /// directly and isntead of relying on the public certificate
        /// infrastructure.
        /// </summary>
        public bool ValidateCertificates { get; set; }

        /// <summary>
        /// Compatibility settings. Can be used to make AuthServices accept
        /// certain non-standard behaviour.
        /// </summary>
        public Compatibility Compatibility { get; set; }
    }
}

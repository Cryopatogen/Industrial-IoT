﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Opc.Ua.PubSub {
    using Opc.Ua;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Encodeable Network message
    /// </summary>
    public class NetworkMessage : IEncodeable {

        /// <summary>
        /// Subscription id
        /// </summary>
        public string SubscriptionId { get; set; }

        /// <summary>
        /// Message content
        /// </summary>
        public uint MessageContentMask { get; set; }

        /// <summary>
        /// Message id
        /// </summary>
        public string MessageId { get; set; }

        /// <summary>
        /// Message type
        /// </summary>
        public string MessageType { get; set; }

        /// <summary>
        /// Publisher identifier
        /// </summary>
        public string PublisherId { get; set; }

        /// <summary>
        /// Dataset class
        /// </summary>
        public string DataSetClassId { get; set; }

        /// <summary>
        /// Message context
        /// </summary>
        public ServiceMessageContext MessageContext { get; set; }

        /// <summary>
        /// Dataset Messages
        /// </summary>
        public List<DataSetMessage> Messages { get; set; }

        /// <inheritdoc/>
        public ExpandedNodeId TypeId => ExpandedNodeId.Null;

        /// <inheritdoc/>
        public ExpandedNodeId BinaryEncodingId => ExpandedNodeId.Null;

        /// <inheritdoc/>
        public ExpandedNodeId XmlEncodingId => ExpandedNodeId.Null;

        /// <inheritdoc/>
        public void Decode(IDecoder decoder) {
            switch (decoder.EncodingType) {
                case EncodingType.Binary:
                    DecodeBinary(decoder);
                    break;
                case EncodingType.Json:
                    DecodeJson(decoder);
                    break;
                case EncodingType.Xml:
                    throw new NotSupportedException("XML encoding is not supported.");
                default:
                    throw new NotImplementedException(
                        $"Unknown encoding: {decoder.EncodingType}");
            }
        }

        /// <inheritdoc/>
        public void Encode(IEncoder encoder) {
            switch (encoder.EncodingType) {
                case EncodingType.Binary:
                    EncodeBinary(encoder);
                    break;
                case EncodingType.Json:
                    EncodeJson(encoder);
                    break;
                case EncodingType.Xml:
                    throw new NotSupportedException("XML encoding is not supported.");
                default:
                    throw new NotImplementedException(
                        $"Unknown encoding: {encoder.EncodingType}");
            }
        }

        /// <inheritdoc/>
        public bool IsEqual(IEncodeable encodeable) {
            return Utils.IsEqual(this, encodeable);
        }

        /// <summary>
        /// Decode from binary
        /// </summary>
        /// <param name="decoder"></param>
        private void DecodeBinary(IDecoder decoder) {
            // TODO
            throw new NotImplementedException();
        }

        /// <summary>
        /// Decode from json
        /// </summary>
        /// <param name="decoder"></param>
        private void DecodeJson(IDecoder decoder) {
            // TODO
            throw new NotImplementedException();
        }

        /// <summary>
        /// Encode as binary
        /// </summary>
        /// <param name="encoder"></param>
        private void EncodeBinary(IEncoder encoder) {
            // TODO
            throw new NotImplementedException();
        }

        /// <summary>
        /// Encode as json
        /// </summary>
        /// <param name="encoder"></param>
        private void EncodeJson(IEncoder encoder) {
            if ((MessageContentMask & (uint)JsonNetworkMessageContentMask.NetworkMessageHeader) != 0) {
                encoder.WriteString(nameof(MessageId), MessageId);
                encoder.WriteString("MessageType", "ua-data");
                if ((MessageContentMask & (uint)JsonNetworkMessageContentMask.PublisherId) != 0) {
                    encoder.WriteString(nameof(PublisherId), PublisherId);
                }
                if ((MessageContentMask & (uint)JsonNetworkMessageContentMask.DataSetClassId) != 0) {
                    encoder.WriteString(nameof(DataSetClassId), DataSetClassId);
                }
                if (Messages != null && Messages.Count > 0) {
                    if ((MessageContentMask & (uint)JsonNetworkMessageContentMask.SingleDataSetMessage) != 0) {
                        encoder.WriteEncodeable(nameof(Messages), Messages[0], typeof(DataSetMessage));
                    }
                    else {
                        encoder.WriteEncodeableArray(nameof(Messages), Messages.ToArray(), typeof(DataSetMessage[]));
                    }
                }
            }
        }
    }
}
﻿//-----------------------------------------------------------------------------
// Filename: RtpIceChannelUnitTest.cs
//
// Description: Unit tests for the RtpIceChannel class.
//
// History:
// 21 Mar 2020	Aaron Clauson	Created.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SIPSorcery.Sys;
using Xunit;

namespace SIPSorcery.Net.UnitTests
{
    [Trait("Category", "unit")]
    public class RtpIceChannelUnitTest
    {
        private Microsoft.Extensions.Logging.ILogger logger = null;

        public RtpIceChannelUnitTest(Xunit.Abstractions.ITestOutputHelper output)
        {
            logger = SIPSorcery.UnitTests.TestLogHelper.InitTestLogger(output);
        }

        /// <summary>
        /// Tests that creating a new IceSession instance works correctly.
        /// </summary>
        [Fact]
        public void CreateInstanceUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            RTPSession rtpSession = new RTPSession(true, true, true);

            // Add a track to the session in order to initialise the RTPChannel.
            MediaStreamTrack dummyTrack = new MediaStreamTrack(SDPMediaTypesEnum.audio, false, new List<SDPMediaFormat> { new SDPMediaFormat(SDPMediaFormatsEnum.PCMU) });
            rtpSession.addTrack(dummyTrack);

            var rtpIceChannel = new RtpIceChannel(null, RTCIceComponent.rtp, null);

            Assert.NotNull(rtpIceChannel);
        }

        /// <summary>
        /// Tests that creating a new IceSession instance and requesting the host candidates works correctly.
        /// </summary>
        [Fact]
        public void GetHostCandidatesUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            var rtpIceChannel = new RtpIceChannel(null, RTCIceComponent.rtp, null);

            logger.LogDebug($"RTP ICE channel RTP socket local end point {rtpIceChannel.RTPLocalEndPoint}.");

            rtpIceChannel.StartGathering();

            Assert.NotNull(rtpIceChannel);
            Assert.NotEmpty(rtpIceChannel.Candidates);

            foreach (var hostCandidate in rtpIceChannel.Candidates)
            {
                logger.LogDebug(hostCandidate.ToString());
            }
        }

        /// <summary>
        /// Tests that creating a new IceSession instance and requesting the host candidates works correctly
        /// when the RTP channel was bound to a single IP address.
        /// </summary>
        [Fact]
        public void GetHostCandidatesForRTPBindUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            var localAddress = NetServices.InternetDefaultAddress;
            var rtpIceChannel = new RtpIceChannel(localAddress, RTCIceComponent.rtp, null);

            logger.LogDebug($"RTP ICE channel RTP socket local end point {rtpIceChannel.RTPLocalEndPoint}.");

            rtpIceChannel.StartGathering();

            Assert.NotNull(rtpIceChannel);
            Assert.NotEmpty(rtpIceChannel.Candidates);
            Assert.True(localAddress.Equals(IPAddress.Parse(rtpIceChannel.Candidates.Single().address)));

            foreach (var hostCandidate in rtpIceChannel.Candidates)
            {
                logger.LogDebug(hostCandidate.ToString());
            }
        }

        /// <summary>
        /// Tests that once remote candidates are added to the RTP ICE channel the checklist stays
        /// in priority sorted order.
        /// </summary>
        [Fact]
        public void SortChecklistUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            var rtpIceChannel = new RtpIceChannel(null, RTCIceComponent.rtp, null);
            rtpIceChannel.StartGathering();

            Assert.NotNull(rtpIceChannel);
            Assert.NotEmpty(rtpIceChannel.Candidates);

            foreach (var hostCandidate in rtpIceChannel.Candidates)
            {
                logger.LogDebug(hostCandidate.ToString());
            }

            var remoteCandidate = RTCIceCandidate.Parse("candidate:408132416 1 udp 2113937151 192.168.11.50 51268 typ host generation 0 ufrag CI7o network-cost 999");
            rtpIceChannel.AddRemoteCandidate(remoteCandidate);

            var remoteCandidate2 = RTCIceCandidate.Parse("candidate:408132417 1 udp 2113937150 192.168.11.51 51268 typ host generation 0 ufrag CI7o network-cost 999");
            rtpIceChannel.AddRemoteCandidate(remoteCandidate2);

            foreach (var entry in rtpIceChannel._checklist)
            {
                logger.LogDebug($"checklist entry priority {entry.Priority}.");
            }
        }

        /// <summary>
        /// Tests that checklist entries get added correctly and duplicates are excluded.
        /// </summary>
        [Fact]
        public async void ChecklistConstructionUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            var rtpIceChannel = new RtpIceChannel(null, RTCIceComponent.rtp, null);
            rtpIceChannel.StartGathering();

            Assert.NotNull(rtpIceChannel);
            Assert.NotEmpty(rtpIceChannel.Candidates);

            foreach (var hostCandidate in rtpIceChannel.Candidates)
            {
                logger.LogDebug($"host candidate: {hostCandidate}");
            }

            var remoteCandidate = RTCIceCandidate.Parse("candidate:408132416 1 udp 2113937151 192.168.11.50 51268 typ host generation 0 ufrag CI7o network-cost 999");
            rtpIceChannel.AddRemoteCandidate(remoteCandidate);

            var remoteCandidate2 = RTCIceCandidate.Parse("candidate:408132417 1 udp 2113937150 192.168.11.50 51268 typ host generation 0 ufrag CI7o network-cost 999");
            rtpIceChannel.AddRemoteCandidate(remoteCandidate2);

            await Task.Delay(500);

            foreach (var entry in rtpIceChannel._checklist)
            {
                logger.LogDebug($"checklist entry: {entry.LocalCandidate.ToShortString()}->{entry.RemoteCandidate.ToShortString()}");
            }

            Assert.Single(rtpIceChannel._checklist);
        }

        /// <summary>
        /// Tests that checklist gets processed and the status of the entry's gets updated as expected.
        /// </summary>
        [Fact]
        public async void ChecklistProcessingUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            var rtpIceChannel = new RtpIceChannel(null, RTCIceComponent.rtp, null);
            rtpIceChannel.StartGathering();

            Assert.NotNull(rtpIceChannel);
            Assert.NotEmpty(rtpIceChannel.Candidates);

            foreach (var hostCandidate in rtpIceChannel.Candidates)
            {
                logger.LogDebug($"host candidate: {hostCandidate}");
            }

            var remoteCandidate = RTCIceCandidate.Parse("candidate:408132416 1 udp 2113937151 192.168.11.50 51268 typ host generation 0 ufrag CI7o network-cost 999");
            rtpIceChannel.AddRemoteCandidate(remoteCandidate);

            rtpIceChannel.SetRemoteCredentials("CI7o", "xxxxxxxxxxxx");
            rtpIceChannel.StartGathering();

            await Task.Delay(2000);

            var checklistEntry = rtpIceChannel._checklist.Single();

            logger.LogDebug($"Checklist entry state {checklistEntry.State}, last check sent at {checklistEntry.LastCheckSentAt}.");

            Assert.Equal(ChecklistEntryState.InProgress, checklistEntry.State);
        }

        /// <summary>
        /// Tests that checklist gets processed and an entry that gets no response ends up in the failed state.
        /// </summary>
        [Fact]
        public async void ChecklistProcessingToFailStateUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            var rtpIceChannel = new RtpIceChannel(null, RTCIceComponent.rtp, null);
            rtpIceChannel.StartGathering();

            Assert.NotNull(rtpIceChannel);
            Assert.NotEmpty(rtpIceChannel.Candidates);

            foreach (var hostCandidate in rtpIceChannel.Candidates)
            {
                logger.LogDebug($"host candidate: {hostCandidate}");
            }

            var remoteCandidate = RTCIceCandidate.Parse("candidate:408132416 1 udp 2113937151 192.168.11.50 51268 typ host generation 0 ufrag CI7o network-cost 999");
            rtpIceChannel.AddRemoteCandidate(remoteCandidate);

            rtpIceChannel.SetRemoteCredentials("CI7o", "xxxxxxxxxxxx");

            logger.LogDebug($"ICE session retry interval {rtpIceChannel.RTO}ms.");

            // The defaults are 5 STUN requests and for a checklist with one entry they will be 500ms apart.
            await Task.Delay(4000);

            Assert.Equal(ChecklistEntryState.Failed, rtpIceChannel._checklist.Single().State);
            Assert.Equal(ChecklistState.Failed, rtpIceChannel._checklistState);
            Assert.Equal(RTCIceConnectionState.failed, rtpIceChannel.IceConnectionState);
        }

        /// <summary>
        /// Tests that two ICE channels with only host candidates are able to successfully
        /// connect.
        /// </summary>
        [Fact]
        public async void CheckSuccessfulConnectionForHostCandidatesUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            var rtpIceChannelA = new RtpIceChannel();
            rtpIceChannelA.IsController = true;
            logger.LogDebug($"RTP ICE channel RTP socket local end point {rtpIceChannelA.RTPLocalEndPoint}.");

            var rtpIceChannelB = new RtpIceChannel();
            logger.LogDebug($"RTP ICE channel RTP socket local end point {rtpIceChannelB.RTPLocalEndPoint}.");

            rtpIceChannelA.StartGathering();
            rtpIceChannelB.StartGathering();

            Assert.NotEmpty(rtpIceChannelA.Candidates);
            Assert.NotEmpty(rtpIceChannelB.Candidates);

            // Because there are no ICE servers gathering completes after the host candidates are gathered.
            Assert.Equal(RTCIceGatheringState.complete, rtpIceChannelA.IceGatheringState);
            Assert.Equal(RTCIceGatheringState.complete, rtpIceChannelB.IceGatheringState);
            Assert.Equal(RTCIceConnectionState.@new, rtpIceChannelA.IceConnectionState);
            Assert.Equal(RTCIceConnectionState.@new, rtpIceChannelB.IceConnectionState);

            // Exchange ICE user and passwords.
            rtpIceChannelA.SetRemoteCredentials(rtpIceChannelB.LocalIceUser, rtpIceChannelB.LocalIcePassword);
            rtpIceChannelB.SetRemoteCredentials(rtpIceChannelA.LocalIceUser, rtpIceChannelA.LocalIcePassword);

            Assert.Equal(RTCIceConnectionState.checking, rtpIceChannelA.IceConnectionState);
            Assert.Equal(RTCIceConnectionState.checking, rtpIceChannelB.IceConnectionState);

            // Exchange ICE candidates.
            rtpIceChannelA.Candidates.ForEach(x => rtpIceChannelB.AddRemoteCandidate(x));
            rtpIceChannelB.Candidates.ForEach(x => rtpIceChannelA.AddRemoteCandidate(x));

            await Task.Delay(500);

            Assert.Equal(RTCIceConnectionState.connected, rtpIceChannelA.IceConnectionState);
            Assert.Equal(RTCIceConnectionState.connected, rtpIceChannelB.IceConnectionState);
            Assert.NotNull(rtpIceChannelA.NominatedEntry);
            Assert.NotNull(rtpIceChannelB.NominatedEntry);
        }

        /// <summary>
        /// Tests that an RTP ICE channel can get a server reflexive candidate from a mock STUN server.
        /// </summary>
        [Fact]
        public async void CheckStunServerGetServerRefelxiveCandidateUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            using (MockTurnServer mockStunServer = new MockTurnServer())
            {
                var iceServers = new List<RTCIceServer> {
                new RTCIceServer
                    {
                        urls = $"stun:{mockStunServer.ListeningEndPoint}",
                    }
                };
                var rtpIceChannel = new RtpIceChannel(null, RTCIceComponent.rtp, iceServers);
                logger.LogDebug($"RTP ICE channel RTP socket local end point {rtpIceChannel.RTPLocalEndPoint}.");

                rtpIceChannel.StartGathering();

                Assert.NotEmpty(rtpIceChannel.Candidates);

                // Because there is an ICE server gathering should still be in progress.
                Assert.Equal(RTCIceGatheringState.gathering, rtpIceChannel.IceGatheringState);
                Assert.Equal(RTCIceConnectionState.@new, rtpIceChannel.IceConnectionState);

                await Task.Delay(500);

                // The STUN server check should now have completed and a server reflexive candidate
                // been acquired

                Assert.Equal(RTCIceGatheringState.complete, rtpIceChannel.IceGatheringState);
                // The connection state stays in "new" because no remote ICE user and password has been set.
                Assert.Equal(RTCIceConnectionState.@new, rtpIceChannel.IceConnectionState);
                Assert.Contains(rtpIceChannel.Candidates, x => x.type == RTCIceCandidateType.srflx);
            }
        }

        /// <summary>
        /// Tests that an RTP ICE channel can get a relay candidate from a mock TURN server.
        /// </summary>
        [Fact]
        public async void CheckTurnServerGetRelayCandidateUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            using (MockTurnServer mockTurnServer = new MockTurnServer())
            {
                var iceServers = new List<RTCIceServer> {
                new RTCIceServer
                    {
                        urls = $"turn:{mockTurnServer.ListeningEndPoint}",
                    }
                };
                var rtpIceChannel = new RtpIceChannel(null, RTCIceComponent.rtp, iceServers);
                logger.LogDebug($"RTP ICE channel RTP socket local end point {rtpIceChannel.RTPLocalEndPoint}.");

                rtpIceChannel.StartGathering();

                Assert.NotEmpty(rtpIceChannel.Candidates);

                // Because there is an ICE server gathering should still be in progress.
                Assert.Equal(RTCIceGatheringState.gathering, rtpIceChannel.IceGatheringState);
                Assert.Equal(RTCIceConnectionState.@new, rtpIceChannel.IceConnectionState);

                await Task.Delay(500);

                // The STUN server check should now have completed and a server reflexive candidate
                // been acquired

                Assert.Equal(RTCIceGatheringState.complete, rtpIceChannel.IceGatheringState);
                // The connection state stays in "new" because no remote ICE user and password has been set.
                Assert.Equal(RTCIceConnectionState.@new, rtpIceChannel.IceConnectionState);
                Assert.Contains(rtpIceChannel.Candidates, x => x.type == RTCIceCandidateType.relay);
            }
        }

        /// <summary>
        /// Tests that two ICE channels with one having a relay only policy candidates are able 
        /// to successfully connect.
        /// </summary>
        [Fact]
        public async void CheckSuccessfulConnectionForRelayCandidatesUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            using (MockTurnServer mockTurnServer = new MockTurnServer())
            {
                var iceServers = new List<RTCIceServer> {
                new RTCIceServer
                    {
                        urls = $"turn:{mockTurnServer.ListeningEndPoint}",
                    }
                };
                var rtpIceChannelRelay = new RtpIceChannel(null, RTCIceComponent.rtp, iceServers, RTCIceTransportPolicy.relay);
                rtpIceChannelRelay.IsController = true;
                logger.LogDebug($"RTP ICE channel RTP socket local end point {rtpIceChannelRelay.RTPLocalEndPoint}.");

                var rtpIceChannelHost = new RtpIceChannel();
                logger.LogDebug($"RTP ICE channel RTP socket local end point {rtpIceChannelHost.RTPLocalEndPoint}.");

                rtpIceChannelRelay.StartGathering();
                rtpIceChannelHost.StartGathering();

                // Need to give some time for the relay channel to connect to the mock TURN server.
                await Task.Delay(200);

                Assert.Single(rtpIceChannelRelay.Candidates);   // Should only have the single local relay candidate.
                Assert.NotEmpty(rtpIceChannelHost.Candidates);
                Assert.Equal(RTCIceGatheringState.complete, rtpIceChannelRelay.IceGatheringState);
                Assert.Equal(RTCIceConnectionState.@new, rtpIceChannelRelay.IceConnectionState);
                Assert.Equal(RTCIceGatheringState.complete, rtpIceChannelHost.IceGatheringState);
                Assert.Equal(RTCIceConnectionState.@new, rtpIceChannelHost.IceConnectionState);

                // Exchange ICE user and passwords.
                rtpIceChannelRelay.SetRemoteCredentials(rtpIceChannelHost.LocalIceUser, rtpIceChannelHost.LocalIcePassword);
                rtpIceChannelHost.SetRemoteCredentials(rtpIceChannelRelay.LocalIceUser, rtpIceChannelRelay.LocalIcePassword);

                Assert.Equal(RTCIceConnectionState.checking, rtpIceChannelRelay.IceConnectionState);
                Assert.Equal(RTCIceConnectionState.checking, rtpIceChannelHost.IceConnectionState);

                // Exchange ICE candidates.
                rtpIceChannelRelay.Candidates.ForEach(x => rtpIceChannelHost.AddRemoteCandidate(x));
                rtpIceChannelHost.Candidates.ForEach(x => rtpIceChannelRelay.AddRemoteCandidate(x));

                await Task.Delay(1000);

                Assert.Equal(RTCIceConnectionState.connected, rtpIceChannelRelay.IceConnectionState);
                Assert.Equal(RTCIceConnectionState.connected, rtpIceChannelHost.IceConnectionState);
                Assert.NotNull(rtpIceChannelRelay.NominatedEntry);
                Assert.NotNull(rtpIceChannelHost.NominatedEntry);
            }
        }
    }
}

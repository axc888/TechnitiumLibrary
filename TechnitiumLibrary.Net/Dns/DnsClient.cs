﻿/*
Technitium Library
Copyright (C) 2018  Shreyas Zare (shreyas@technitium.com)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using TechnitiumLibrary.Net.Proxy;

namespace TechnitiumLibrary.Net.Dns
{
    public class DnsClient
    {
        #region variables

        public static readonly NameServerAddress[] ROOT_NAME_SERVERS_IPv4;
        public static readonly NameServerAddress[] ROOT_NAME_SERVERS_IPv6;

        readonly internal static RandomNumberGenerator _rnd = new RNGCryptoServiceProvider();

        const int MAX_HOPS = 16;

        readonly NameServerAddress[] _servers;

        NetProxy _proxy;
        bool _preferIPv6 = false;
        bool _tcp = false;
        int _retries = 2;
        int _connectionTimeout = 2000;
        int _sendTimeout = 2000;
        int _recvTimeout = 2000;

        #endregion

        #region constructor

        static DnsClient()
        {
            ROOT_NAME_SERVERS_IPv4 = new NameServerAddress[]
            {
                new NameServerAddress("a.root-servers.net", IPAddress.Parse("198.41.0.4")), //VeriSign, Inc.
                new NameServerAddress("b.root-servers.net", IPAddress.Parse("192.228.79.201")), //University of Southern California (ISI)
                new NameServerAddress("c.root-servers.net", IPAddress.Parse("192.33.4.12")), //Cogent Communications
                new NameServerAddress("d.root-servers.net", IPAddress.Parse("199.7.91.13")), //University of Maryland
                new NameServerAddress("e.root-servers.net", IPAddress.Parse("192.203.230.10")), //NASA (Ames Research Center)
                new NameServerAddress("f.root-servers.net", IPAddress.Parse("192.5.5.241")), //Internet Systems Consortium, Inc.
                new NameServerAddress("g.root-servers.net", IPAddress.Parse("192.112.36.4")), //US Department of Defense (NIC)
                new NameServerAddress("h.root-servers.net", IPAddress.Parse("198.97.190.53")), //US Army (Research Lab)
                new NameServerAddress("i.root-servers.net", IPAddress.Parse("192.36.148.17")), //Netnod
                new NameServerAddress("j.root-servers.net", IPAddress.Parse("192.58.128.30")), //VeriSign, Inc.
                new NameServerAddress("k.root-servers.net", IPAddress.Parse("193.0.14.129")), //RIPE NCC
                new NameServerAddress("l.root-servers.net", IPAddress.Parse("199.7.83.42")), //ICANN
                new NameServerAddress("m.root-servers.net", IPAddress.Parse("202.12.27.33")) //WIDE Project
            };

            ROOT_NAME_SERVERS_IPv6 = new NameServerAddress[]
            {
                new NameServerAddress("a.root-servers.net", IPAddress.Parse("2001:503:ba3e::2:30")), //VeriSign, Inc.
                new NameServerAddress("b.root-servers.net", IPAddress.Parse("2001:500:84::b")), //University of Southern California (ISI)
                new NameServerAddress("c.root-servers.net", IPAddress.Parse("2001:500:2::c")), //Cogent Communications
                new NameServerAddress("d.root-servers.net", IPAddress.Parse("2001:500:2d::d")), //University of Maryland
                new NameServerAddress("e.root-servers.net", IPAddress.Parse("2001:500:a8::e")), //NASA (Ames Research Center)
                new NameServerAddress("f.root-servers.net", IPAddress.Parse("2001:500:2f::f")), //Internet Systems Consortium, Inc.
                new NameServerAddress("g.root-servers.net", IPAddress.Parse("2001:500:12::d0d")), //US Department of Defense (NIC)
                new NameServerAddress("h.root-servers.net", IPAddress.Parse("2001:500:1::53")), //US Army (Research Lab)
                new NameServerAddress("i.root-servers.net", IPAddress.Parse("2001:7fe::53")), //Netnod
                new NameServerAddress("j.root-servers.net", IPAddress.Parse("2001:503:c27::2:30")), //VeriSign, Inc.
                new NameServerAddress("k.root-servers.net", IPAddress.Parse("2001:7fd::1")), //RIPE NCC
                new NameServerAddress("l.root-servers.net", IPAddress.Parse("2001:500:9f::42")), //ICANN
                new NameServerAddress("m.root-servers.net", IPAddress.Parse("2001:dc3::35")) //WIDE Project
            };
        }

        public DnsClient(bool preferIPv6 = false)
        {
            _preferIPv6 = preferIPv6;

            NetworkInfo defaultNetworkInfo;

            if (_preferIPv6)
            {
                defaultNetworkInfo = NetUtilities.GetDefaultIPv6NetworkInfo();

                if ((defaultNetworkInfo == null) || (defaultNetworkInfo.Interface.GetIPProperties().DnsAddresses.Count == 0))
                    defaultNetworkInfo = NetUtilities.GetDefaultIPv4NetworkInfo();
            }
            else
            {
                defaultNetworkInfo = NetUtilities.GetDefaultIPv4NetworkInfo();
            }

            if (defaultNetworkInfo == null)
                throw new DnsClientException("No default network connection was found on this computer.");

            IPAddressCollection servers = defaultNetworkInfo.Interface.GetIPProperties().DnsAddresses;

            if (servers.Count == 0)
                throw new DnsClientException("Default network does not have any DNS server configured.");

            _servers = new NameServerAddress[servers.Count];

            for (int i = 0; i < servers.Count; i++)
                _servers[i] = new NameServerAddress(servers[i]);
        }

        public DnsClient(IPAddress[] servers)
        {
            if (servers.Length == 0)
                throw new DnsClientException("Atleast one name server must be available for DnsClient.");

            _servers = new NameServerAddress[servers.Length];

            for (int i = 0; i < servers.Length; i++)
                _servers[i] = new NameServerAddress(servers[i]);
        }

        public DnsClient(IPAddress server)
            : this(new NameServerAddress(server))
        { }

        public DnsClient(IPEndPoint server)
            : this(new NameServerAddress(server))
        { }

        public DnsClient(NameServerAddress server)
        {
            _servers = new NameServerAddress[] { server };
        }

        public DnsClient(NameServerAddress[] servers)
        {
            if (servers.Length == 0)
                throw new DnsClientException("Atleast one name server must be available for DnsClient.");

            _servers = servers;
        }

        #endregion

        #region static

        public static DnsDatagram ResolveViaRootNameServers(string domain, DnsResourceRecordType queryType, IDnsCache cache = null, NetProxy proxy = null, bool preferIPv6 = false, bool tcp = false, int retries = 2, int maxStackCount = 10)
        {
            return ResolveViaNameServers(domain, queryType, null, cache, proxy, preferIPv6, tcp, retries, maxStackCount);
        }

        public static DnsDatagram ResolveViaNameServers(string domain, DnsResourceRecordType queryType, NameServerAddress[] nameServers = null, IDnsCache cache = null, NetProxy proxy = null, bool preferIPv6 = false, bool tcp = false, int retries = 2, int maxStackCount = 10)
        {
            DnsQuestionRecord question;

            if (queryType == DnsResourceRecordType.PTR)
                question = new DnsQuestionRecord(IPAddress.Parse(domain), DnsClass.IN);
            else
                question = new DnsQuestionRecord(domain, queryType, DnsClass.IN);

            return ResolveViaNameServers(question, nameServers, cache, proxy, preferIPv6, tcp, retries, maxStackCount);
        }

        public static DnsDatagram ResolveViaNameServers(DnsQuestionRecord question, NameServerAddress[] nameServers = null, IDnsCache cache = null, NetProxy proxy = null, bool preferIPv6 = false, bool tcp = false, int retries = 2, int maxStackCount = 10)
        {
            if ((nameServers != null) && (nameServers.Length > 0))
            {
                //create copy of name servers array so that the values in original array are not messed due to shuffling feature
                NameServerAddress[] nameServersCopy = new NameServerAddress[nameServers.Length];
                Array.Copy(nameServers, nameServersCopy, nameServers.Length);
                nameServers = nameServersCopy;
            }

            Stack<ResolverData> resolverStack = new Stack<ResolverData>();
            int stackNameServerIndex = 0;

            while (true) //stack loop
            {
                if (resolverStack.Count > maxStackCount)
                {
                    while (resolverStack.Count > 0)
                    {
                        ResolverData data = resolverStack.Pop();

                        question = data.Question;
                    }

                    throw new DnsClientException("DnsClient exceeded the maximum stack count to resolve the domain: " + question.Name);
                }

                if (cache != null)
                {
                    DnsDatagram request = new DnsDatagram(new DnsHeader(0, false, DnsOpcode.StandardQuery, false, false, true, false, false, false, DnsResponseCode.NoError, 1, 0, 0, 0), new DnsQuestionRecord[] { question }, null, null, null);
                    DnsDatagram cacheResponse = cache.Query(request);

                    switch (cacheResponse.Header.RCODE)
                    {
                        case DnsResponseCode.NoError:
                            if (cacheResponse.Answer.Length > 0)
                            {
                                if (resolverStack.Count == 0)
                                {
                                    return cacheResponse;
                                }
                                else
                                {
                                    ResolverData data = resolverStack.Pop();

                                    question = data.Question;
                                    nameServers = data.NameServers;
                                    stackNameServerIndex = data.NameServerIndex;

                                    switch (cacheResponse.Answer[0].Type)
                                    {
                                        case DnsResourceRecordType.AAAA:
                                            nameServers[stackNameServerIndex] = new NameServerAddress(nameServers[stackNameServerIndex].Domain, (cacheResponse.Answer[0].RDATA as DnsAAAARecord).Address);
                                            break;

                                        case DnsResourceRecordType.A:
                                            nameServers[stackNameServerIndex] = new NameServerAddress(nameServers[stackNameServerIndex].Domain, (cacheResponse.Answer[0].RDATA as DnsARecord).Address);
                                            break;

                                        default:
                                            //didnt find IP for current name server
                                            stackNameServerIndex++; //increment to skip current name server
                                            break;
                                    }

                                    continue; //stack loop
                                }
                            }

                            if (cacheResponse.Authority.Length > 0)
                            {
                                if (cacheResponse.Authority[0].Type == DnsResourceRecordType.SOA)
                                {
                                    if (resolverStack.Count == 0)
                                    {
                                        return cacheResponse;
                                    }
                                    else
                                    {
                                        if (question.Type == DnsResourceRecordType.AAAA)
                                        {
                                            question = new DnsQuestionRecord(question.Name, DnsResourceRecordType.A, question.Class);
                                        }
                                        else
                                        {
                                            //didnt find IP for current name server
                                            //pop and try next name server
                                            ResolverData data = resolverStack.Pop();

                                            question = data.Question;
                                            nameServers = data.NameServers;
                                            stackNameServerIndex = data.NameServerIndex + 1; //increment to skip current name server
                                        }

                                        continue; //to stack loop
                                    }
                                }

                                if ((nameServers == null) || (nameServers.Length == 0))
                                {
                                    NameServerAddress[] cacheNameServers = NameServerAddress.GetNameServersFromResponse(cacheResponse, preferIPv6, true);

                                    if (cacheNameServers.Length > 0)
                                        nameServers = cacheNameServers;
                                }
                            }

                            break;

                        case DnsResponseCode.NameError:
                            if (resolverStack.Count == 0)
                            {
                                return cacheResponse;
                            }
                            else
                            {
                                //current name server domain doesnt exists
                                //pop and try next name server
                                ResolverData data = resolverStack.Pop();

                                question = data.Question;
                                nameServers = data.NameServers;
                                stackNameServerIndex = data.NameServerIndex + 1; //increment to skip current name server

                                continue; //stack loop
                            }
                    }
                }

                if ((nameServers == null) || (nameServers.Length == 0))
                {
                    //create copy of root name servers array so that the values in original array are not messed due to shuffling feature
                    if (preferIPv6)
                    {
                        nameServers = new NameServerAddress[ROOT_NAME_SERVERS_IPv6.Length];
                        Array.Copy(ROOT_NAME_SERVERS_IPv6, nameServers, ROOT_NAME_SERVERS_IPv6.Length);
                    }
                    else
                    {
                        nameServers = new NameServerAddress[ROOT_NAME_SERVERS_IPv4.Length];
                        Array.Copy(ROOT_NAME_SERVERS_IPv4, nameServers, ROOT_NAME_SERVERS_IPv4.Length);
                    }
                }

                NameServerAddress.Shuffle(nameServers);

                int hopCount = 0;
                while ((hopCount++) < MAX_HOPS) //resolver loop
                {
                    //copy and reset stack name server index since its one time use only after stack pop
                    int i = stackNameServerIndex;
                    stackNameServerIndex = 0;

                    //query name servers one by one
                    for (; i < nameServers.Length; i++) //retry next server loop
                    {
                        NameServerAddress currentNameServer = nameServers[i];

                        if (question.Name == currentNameServer.Domain)
                            continue; //obvious!

                        if (currentNameServer.EndPoint == null)
                        {
                            resolverStack.Push(new ResolverData(question, nameServers, i));

                            if (preferIPv6)
                                question = new DnsQuestionRecord(currentNameServer.Domain, DnsResourceRecordType.AAAA, question.Class);
                            else
                                question = new DnsQuestionRecord(currentNameServer.Domain, DnsResourceRecordType.A, question.Class);

                            nameServers = null;
                            goto stackLoop;
                        }

                        DnsClient client = new DnsClient(currentNameServer);

                        client._proxy = proxy;
                        client._preferIPv6 = preferIPv6;
                        client._tcp = tcp;
                        client._retries = retries;

                        DnsDatagram request = new DnsDatagram(new DnsHeader(0, false, DnsOpcode.StandardQuery, false, false, true, false, false, false, DnsResponseCode.NoError, 1, 0, 0, 0), new DnsQuestionRecord[] { question }, null, null, null);
                        DnsDatagram response;

                        try
                        {
                            response = client.Resolve(request);
                        }
                        catch (DnsClientException)
                        {
                            continue; //resolver loop
                        }

                        if (response.Header.Truncation)
                        {
                            if (tcp)
                                return response;

                            client._tcp = true;
                            response = client.Resolve(request);
                        }

                        if (cache != null)
                            cache.CacheResponse(response);

                        switch (response.Header.RCODE)
                        {
                            case DnsResponseCode.NoError:
                                if (response.Answer.Length > 0)
                                {
                                    if (!response.Answer[0].Name.Equals(question.Name, StringComparison.CurrentCultureIgnoreCase))
                                        continue; //continue to next name server since current name server may be misconfigured

                                    if (resolverStack.Count == 0)
                                    {
                                        return response;
                                    }
                                    else
                                    {
                                        ResolverData data = resolverStack.Pop();

                                        question = data.Question;
                                        nameServers = data.NameServers;
                                        stackNameServerIndex = data.NameServerIndex;

                                        switch (response.Answer[0].Type)
                                        {
                                            case DnsResourceRecordType.AAAA:
                                                nameServers[stackNameServerIndex] = new NameServerAddress(nameServers[stackNameServerIndex].Domain, (response.Answer[0].RDATA as DnsAAAARecord).Address);
                                                break;

                                            case DnsResourceRecordType.A:
                                                nameServers[stackNameServerIndex] = new NameServerAddress(nameServers[stackNameServerIndex].Domain, (response.Answer[0].RDATA as DnsARecord).Address);
                                                break;

                                            default:
                                                //didnt find IP for current name server
                                                stackNameServerIndex++; //increment to skip current name server
                                                break;
                                        }

                                        goto resolverLoop;
                                    }
                                }

                                if (response.Authority.Length == 0)
                                    continue; //continue to next name server since current name server may be misconfigured

                                if (response.Authority[0].Type == DnsResourceRecordType.SOA)
                                {
                                    //no entry for given type
                                    if (resolverStack.Count == 0)
                                    {
                                        return response;
                                    }
                                    else
                                    {
                                        if (question.Type == DnsResourceRecordType.AAAA)
                                        {
                                            question = new DnsQuestionRecord(question.Name, DnsResourceRecordType.A, question.Class);
                                        }
                                        else
                                        {
                                            //didnt find IP for current name server
                                            //pop and try next name server
                                            ResolverData data = resolverStack.Pop();

                                            question = data.Question;
                                            nameServers = data.NameServers;
                                            stackNameServerIndex = data.NameServerIndex + 1; //increment to skip current name server
                                        }

                                        goto stackLoop; //goto stack loop
                                    }
                                }

                                nameServers = NameServerAddress.GetNameServersFromResponse(response, preferIPv6, false);

                                if (nameServers.Length == 0)
                                    continue; //continue to next name server since current name server may be misconfigured

                                goto resolverLoop;

                            case DnsResponseCode.NameError:
                                if (resolverStack.Count == 0)
                                {
                                    return response;
                                }
                                else
                                {
                                    //current name server domain doesnt exists
                                    //pop and try next name server
                                    ResolverData data = resolverStack.Pop();

                                    question = data.Question;
                                    nameServers = data.NameServers;
                                    stackNameServerIndex = data.NameServerIndex + 1; //increment to skip current name server

                                    goto stackLoop; //goto stack loop
                                }

                            default:
                                continue; //continue to next name server since current name server may be misconfigured
                        }
                    }

                    if (resolverStack.Count == 0)
                    {
                        throw new DnsClientException("DnsClient failed to resolve the request: no response from name servers.");
                    }
                    else
                    {
                        //didnt find IP for current name server
                        //pop and try next name server
                        ResolverData data = resolverStack.Pop();

                        question = data.Question;
                        nameServers = data.NameServers;
                        stackNameServerIndex = data.NameServerIndex + 1; //increment to skip current name server

                        break; //to stack loop
                    }

                    resolverLoop:;
                }

                stackLoop:;
            }
        }

        #endregion

        #region public

        public DnsDatagram Resolve(DnsDatagram request)
        {
            int bytesRecv;
            byte[] responseBuffer = null;
            int nextServerIndex = 0;
            int retries = _retries;
            byte[] requestBuffer;
            IDnsCache dnsCache = null;

            //serialize request
            using (MemoryStream mS = new MemoryStream(32))
            {
                if (_tcp)
                    mS.Position = 2;

                //write dns datagram
                request.WriteTo(mS);

                requestBuffer = mS.ToArray();

                if (_tcp)
                {
                    byte[] length = BitConverter.GetBytes(Convert.ToUInt16(requestBuffer.Length - 2));

                    requestBuffer[0] = length[1];
                    requestBuffer[1] = length[0];
                }
            }

            //init server selection parameters
            if (_servers.Length > 1)
            {
                retries = retries * _servers.Length; //retries on per server basis

                byte[] select = new byte[1];
                _rnd.GetBytes(select);

                nextServerIndex = select[0] % _servers.Length;
            }

            int retry = 0;
            while (retry < retries)
            {
                //select server
                NameServerAddress server;

                if (_servers.Length > 1)
                {
                    server = _servers[nextServerIndex];
                    nextServerIndex = (nextServerIndex + 1) % _servers.Length;
                }
                else
                {
                    server = _servers[0];
                }

                if (server.EndPoint == null)
                {
                    if (dnsCache == null)
                        dnsCache = new SimpleDnsCache();

                    server.ResolveAddress(dnsCache, _proxy, _preferIPv6, _tcp, _retries);

                    if (server.EndPoint == null)
                    {
                        retry++;
                        continue;
                    }
                }

                //query server
                Socket _socket = null;
                SocksUdpAssociateRequestHandler proxyRequestHandler = null;

                try
                {
                    retry++;

                    DateTime sentAt = DateTime.UtcNow;
                    bool dnsTcp;

                    if (_proxy == null)
                    {
                        if (_tcp)
                        {
                            _socket = new Socket(server.EndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                            _socket.NoDelay = true;
                            _socket.SendTimeout = _sendTimeout;
                            _socket.ReceiveTimeout = _recvTimeout;

                            IAsyncResult result = _socket.BeginConnect(server.EndPoint, null, null);
                            if (!result.AsyncWaitHandle.WaitOne(_connectionTimeout))
                                throw new SocketException((int)SocketError.TimedOut);

                            if (!_socket.Connected)
                                throw new SocketException((int)SocketError.ConnectionRefused);
                        }
                        else
                        {
                            _socket = new Socket(server.EndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

                            _socket.SendTimeout = _sendTimeout;
                            _socket.ReceiveTimeout = _recvTimeout;
                        }

                        dnsTcp = _tcp;
                    }
                    else
                    {
                        switch (_proxy.Type)
                        {
                            case NetProxyType.Http:
                                _socket = _proxy.HttpProxy.Connect(server.EndPoint, _connectionTimeout);

                                _socket.NoDelay = true;
                                _socket.SendTimeout = _sendTimeout;
                                _socket.ReceiveTimeout = _recvTimeout;

                                dnsTcp = true;
                                break;

                            case NetProxyType.Socks5:
                                if (!_tcp)
                                {
                                    try
                                    {
                                        proxyRequestHandler = _proxy.SocksProxy.UdpAssociate(_connectionTimeout);
                                        proxyRequestHandler.ReceiveTimeout = _recvTimeout;

                                        dnsTcp = false;
                                        break;
                                    }
                                    catch (SocksClientException)
                                    { }
                                }

                                using (SocksConnectRequestHandler requestHandler = _proxy.SocksProxy.Connect(server.EndPoint, _connectionTimeout))
                                {
                                    _socket = requestHandler.GetSocket();

                                    _socket.NoDelay = true;
                                    _socket.SendTimeout = _sendTimeout;
                                    _socket.ReceiveTimeout = _recvTimeout;

                                    dnsTcp = true;
                                }

                                break;

                            default:
                                throw new NotSupportedException("Proxy type not supported by DnsClient.");
                        }
                    }

                    if (dnsTcp)
                    {
                        _socket.Send(requestBuffer);

                        if ((responseBuffer == null) || (responseBuffer.Length == 512))
                            responseBuffer = new byte[64 * 1024];

                        bytesRecv = _socket.Receive(responseBuffer, 0, 2, SocketFlags.None);
                        if (bytesRecv < 1)
                            throw new SocketException((int)SocketError.ConnectionReset);

                        Array.Reverse(responseBuffer, 0, 2);
                        ushort length = BitConverter.ToUInt16(responseBuffer, 0);

                        int offset = 0;
                        while (offset < length)
                        {
                            bytesRecv = _socket.Receive(responseBuffer, offset, length, SocketFlags.None);
                            if (bytesRecv < 1)
                                throw new SocketException((int)SocketError.ConnectionReset);

                            offset += bytesRecv;
                        }

                        bytesRecv = length;
                    }
                    else
                    {
                        if (responseBuffer == null)
                            responseBuffer = new byte[512];

                        if (proxyRequestHandler == null)
                        {
                            _socket.SendTo(requestBuffer, server.EndPoint);

                            EndPoint remoteEP;

                            if (server.EndPoint.AddressFamily == AddressFamily.InterNetworkV6)
                                remoteEP = new IPEndPoint(IPAddress.IPv6Any, 0);
                            else
                                remoteEP = new IPEndPoint(IPAddress.Any, 0);

                            bytesRecv = _socket.ReceiveFrom(responseBuffer, ref remoteEP);
                        }
                        else
                        {
                            proxyRequestHandler.SendTo(requestBuffer, 0, requestBuffer.Length, new SocksEndPoint(server.EndPoint));

                            bytesRecv = proxyRequestHandler.ReceiveFrom(responseBuffer, 0, responseBuffer.Length, out SocksEndPoint socksRemoteEP);
                        }
                    }

                    //parse response
                    using (MemoryStream mS = new MemoryStream(responseBuffer, 0, bytesRecv, false))
                    {
                        double rtt = (DateTime.UtcNow - sentAt).TotalMilliseconds;
                        DnsDatagram response = new DnsDatagram(mS, server, (_tcp ? ProtocolType.Tcp : ProtocolType.Udp), rtt);

                        if (response.Header.Identifier == request.Header.Identifier)
                            return response;
                    }
                }
                catch (SocketException)
                { }
                finally
                {
                    if (_socket != null)
                        _socket.Dispose();

                    if (proxyRequestHandler != null)
                        proxyRequestHandler.Dispose();
                }
            }

            throw new DnsClientException("DnsClient failed to resolve the request: no response from name servers.");
        }

        public DnsDatagram Resolve(DnsQuestionRecord questionRecord)
        {
            return Resolve(new DnsDatagram(new DnsHeader(0, false, DnsOpcode.StandardQuery, false, false, true, false, false, false, DnsResponseCode.NoError, 1, 0, 0, 0), new DnsQuestionRecord[] { questionRecord }, null, null, null));
        }

        public DnsDatagram Resolve(string domain, DnsResourceRecordType queryType)
        {
            if (queryType == DnsResourceRecordType.PTR)
                return Resolve(new DnsQuestionRecord(IPAddress.Parse(domain), DnsClass.IN));
            else
                return Resolve(new DnsQuestionRecord(domain, queryType, DnsClass.IN));
        }

        public string[] ResolveMX(MailAddress emailAddress, bool resolveIP = false, bool preferIPv6 = false)
        {
            return ResolveMX(emailAddress.Host, resolveIP, preferIPv6);
        }

        public string[] ResolveMX(string domain, bool resolveIP = false, bool preferIPv6 = false)
        {
            if (IPAddress.TryParse(domain, out IPAddress parsedIP))
            {
                //host is valid ip address
                return new string[] { domain };
            }

            int hopCount = 0;

            while ((hopCount++) < MAX_HOPS)
            {
                DnsDatagram response = Resolve(new DnsQuestionRecord(domain, DnsResourceRecordType.MX, DnsClass.IN));

                switch (response.Header.RCODE)
                {
                    case DnsResponseCode.NoError:
                        if (response.Header.ANCOUNT == 0)
                            return new string[] { };

                        List<DnsMXRecord> mxRecordsList = new List<DnsMXRecord>();

                        foreach (DnsResourceRecord record in response.Answer)
                        {
                            if (record.Name.Equals(domain, StringComparison.CurrentCultureIgnoreCase))
                            {
                                switch (record.Type)
                                {
                                    case DnsResourceRecordType.MX:
                                        mxRecordsList.Add((DnsMXRecord)record.RDATA);
                                        break;

                                    case DnsResourceRecordType.CNAME:
                                        domain = ((DnsCNAMERecord)record.RDATA).CNAMEDomainName;
                                        break;

                                    default:
                                        throw new DnsClientException("Name server [" + response.NameServerAddress.ToString() + "] returned unexpected record type [" + record.Type.ToString() + "] for domain: " + domain);
                                }
                            }
                        }

                        if (mxRecordsList.Count > 0)
                        {
                            DnsMXRecord[] mxRecords = mxRecordsList.ToArray();

                            //sort by mx preference
                            Array.Sort(mxRecords);

                            if (resolveIP)
                            {
                                List<string> mxEntries = new List<string>();

                                //check glue records
                                for (int i = 0; i < mxRecords.Length; i++)
                                {
                                    string mxDomain = mxRecords[i].Exchange;
                                    bool glueRecordFound = false;

                                    foreach (DnsResourceRecord record in response.Additional)
                                    {
                                        if (record.Name.Equals(mxDomain, StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            switch (record.Type)
                                            {
                                                case DnsResourceRecordType.A:
                                                    if (!preferIPv6)
                                                    {
                                                        mxEntries.Add(((DnsARecord)record.RDATA).Address.ToString());
                                                        glueRecordFound = true;
                                                    }
                                                    break;

                                                case DnsResourceRecordType.AAAA:
                                                    if (preferIPv6)
                                                    {
                                                        mxEntries.Add(((DnsAAAARecord)record.RDATA).Address.ToString());
                                                        glueRecordFound = true;
                                                    }
                                                    break;
                                            }
                                        }
                                    }

                                    if (!glueRecordFound)
                                    {
                                        try
                                        {
                                            IPAddress[] ipList = ResolveIP(mxDomain, preferIPv6);

                                            foreach (IPAddress ip in ipList)
                                                mxEntries.Add(ip.ToString());
                                        }
                                        catch (NameErrorDnsClientException)
                                        { }
                                        catch (DnsClientException)
                                        {
                                            mxEntries.Add(mxDomain);
                                        }
                                    }
                                }

                                return mxEntries.ToArray();
                            }
                            else
                            {
                                string[] mxEntries = new string[mxRecords.Length];

                                for (int i = 0; i < mxRecords.Length; i++)
                                    mxEntries[i] = mxRecords[i].Exchange;

                                return mxEntries;
                            }
                        }

                        break;

                    case DnsResponseCode.NameError:
                        throw new NameErrorDnsClientException("Domain does not exists: " + domain + "; Name server: " + response.NameServerAddress.ToString());

                    default:
                        throw new DnsClientException("Name server returned error. DNS RCODE: " + response.Header.RCODE.ToString() + " (" + response.Header.RCODE + ")");
                }
            }

            throw new DnsClientException("No answer received from name server for domain: " + domain);
        }

        public string ResolvePTR(IPAddress ip)
        {
            DnsDatagram response = Resolve(new DnsQuestionRecord(ip, DnsClass.IN));

            switch (response.Header.RCODE)
            {
                case DnsResponseCode.NoError:
                    if ((response.Header.ANCOUNT > 0) && (response.Answer[0].Type == DnsResourceRecordType.PTR))
                        return ((DnsPTRRecord)response.Answer[0].RDATA).PTRDomainName;

                    return null;

                case DnsResponseCode.NameError:
                    throw new NameErrorDnsClientException("PTR record does not exists for ip: " + ip.ToString() + "; Name server: " + response.NameServerAddress.ToString());

                default:
                    throw new DnsClientException("Name server returned error. DNS RCODE: " + response.Header.RCODE.ToString() + " (" + response.Header.RCODE + ")");
            }
        }

        public IPAddress[] ResolveIP(string domain, bool preferIPv6 = false)
        {
            int hopCount = 0;
            DnsResourceRecordType type = preferIPv6 ? DnsResourceRecordType.AAAA : DnsResourceRecordType.A;

            while ((hopCount++) < MAX_HOPS)
            {
                DnsDatagram response = Resolve(new DnsQuestionRecord(domain, type, DnsClass.IN));

                switch (response.Header.RCODE)
                {
                    case DnsResponseCode.NoError:
                        if (response.Header.ANCOUNT == 0)
                        {
                            if (type == DnsResourceRecordType.AAAA)
                            {
                                type = DnsResourceRecordType.A;
                                continue;
                            }

                            return new IPAddress[] { };
                        }

                        List<IPAddress> ipAddresses = new List<IPAddress>();

                        foreach (DnsResourceRecord record in response.Answer)
                        {
                            if (record.Name.Equals(domain, StringComparison.CurrentCultureIgnoreCase))
                            {
                                switch (record.Type)
                                {
                                    case DnsResourceRecordType.A:
                                        ipAddresses.Add(((DnsARecord)record.RDATA).Address);
                                        break;

                                    case DnsResourceRecordType.AAAA:
                                        ipAddresses.Add(((DnsAAAARecord)record.RDATA).Address);
                                        break;

                                    case DnsResourceRecordType.CNAME:
                                        domain = ((DnsCNAMERecord)record.RDATA).CNAMEDomainName;
                                        break;

                                    default:
                                        throw new DnsClientException("Name server [" + response.NameServerAddress.ToString() + "] returned unexpected record type [ " + record.Type.ToString() + "] for domain: " + domain);
                                }
                            }
                        }

                        if (ipAddresses.Count > 0)
                            return ipAddresses.ToArray();

                        break;

                    case DnsResponseCode.NameError:
                        throw new NameErrorDnsClientException("Domain does not exists: " + domain + "; Name server: " + response.NameServerAddress.ToString());

                    default:
                        throw new DnsClientException("Name server returned error. DNS RCODE: " + response.Header.RCODE.ToString() + " (" + response.Header.RCODE + ")");
                }
            }

            throw new DnsClientException("No answer received from name server for domain: " + domain);
        }

        #endregion

        #region property

        public NameServerAddress[] Servers
        { get { return _servers; } }

        public NetProxy Proxy
        {
            get { return _proxy; }
            set { _proxy = value; }
        }

        public bool PreferIPv6
        {
            get { return _preferIPv6; }
            set { _preferIPv6 = value; }
        }

        public bool Tcp
        {
            get { return _tcp; }
            set { _tcp = value; }
        }

        public int Retries
        {
            get { return _retries; }
            set { _retries = value; }
        }

        public int ConnectionTimeout
        {
            get { return _connectionTimeout; }
            set { _connectionTimeout = value; }
        }

        public int SendTimeout
        {
            get { return _sendTimeout; }
            set { _sendTimeout = value; }
        }

        public int ReceiveTimeout
        {
            get { return _recvTimeout; }
            set { _recvTimeout = value; }
        }

        #endregion

        class ResolverData
        {
            public DnsQuestionRecord Question;
            public NameServerAddress[] NameServers;
            public int NameServerIndex;

            public ResolverData(DnsQuestionRecord question, NameServerAddress[] nameServers, int nameServerIndex)
            {
                this.Question = question;
                this.NameServers = nameServers;
                this.NameServerIndex = nameServerIndex;
            }
        }
    }
}

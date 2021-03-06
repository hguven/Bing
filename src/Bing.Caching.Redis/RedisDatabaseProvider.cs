﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Bing.Caching.Redis
{
    /// <summary>
    /// Redis 数据库提供程序
    /// </summary>
    public class RedisDatabaseProvider:IRedisDatabaseProvider
    {
        /// <summary>
        /// 配置
        /// </summary>
        private readonly RedisCacheOptions _options;

        /// <summary>
        /// Redis多连接复用器
        /// </summary>
        private readonly Lazy<ConnectionMultiplexer> _connectionMultiplexer;

        /// <summary>
        /// Redis 缓存配置
        /// </summary>
        public RedisCacheOptions Options => _options;

        /// <summary>
        /// 初始化一个<see cref="RedisCacheOptions"/>类型的实例
        /// </summary>
        /// <param name="options">Redis缓存配置</param>
        public RedisDatabaseProvider(RedisCacheOptions options)
        {            
            _options = options;            
            _connectionMultiplexer =new Lazy<ConnectionMultiplexer>(CreateConnectionMultiplexer);
        }

        /// <summary>
        /// 获取 数据库
        /// </summary>
        /// <returns></returns>
        public IDatabase GetDatabase()
        {
            return _connectionMultiplexer.Value.GetDatabase(_options.Database);
        }

        /// <summary>
        /// 获取 服务器列表
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IServer> GetServerList()
        {
            var endpoints = GetMastersServersEndpoints();
            foreach (var endPoint in endpoints)
            {
                yield return _connectionMultiplexer.Value.GetServer(endPoint);
            }
        }

        /// <summary>
        /// 获取 Redis多连接复用器
        /// </summary>
        /// <returns></returns>
        public ConnectionMultiplexer GetConnectionMultiplexer()
        {
            return _connectionMultiplexer.Value;
        }

        /// <summary>
        /// 获取当前Redis服务器
        /// </summary>
        /// <param name="hostAndPort">主机名和端口</param>
        /// <returns></returns>
        public IServer GetServer(string hostAndPort)
        {
            return _connectionMultiplexer.Value.GetServer(hostAndPort);
        }

        /// <summary>
        /// 创建Redis多连接复用器
        /// </summary>
        /// <returns></returns>
        private ConnectionMultiplexer CreateConnectionMultiplexer()
        {
            var configurationOptions=new ConfigurationOptions()
            {
                ConnectTimeout = _options.ConnectionTimeout,
                Password = _options.Password,
                Ssl = _options.IsSsl,
                SslHost = _options.SslHost,
                SyncTimeout = _options.SyncTimeout,
                AllowAdmin = _options.AllowAdmin
            };

            foreach (var endPoint in _options.EndPoints)
            {
                configurationOptions.EndPoints.Add(endPoint.Host,endPoint.Port);
            }
            return ConnectionMultiplexer.Connect(configurationOptions.ToString());
        }

        /// <summary>
        /// 获取主服务器端点列表
        /// </summary>
        /// <returns></returns>
        private List<EndPoint> GetMastersServersEndpoints()
        {
            var masters=new List<EndPoint>();
            foreach (var endPoint in _connectionMultiplexer.Value.GetEndPoints())
            {
                var server = _connectionMultiplexer.Value.GetServer(endPoint);
                if (server.IsConnected)
                {
                    // 集群
                    if (server.ServerType == ServerType.Cluster)
                    {
                        masters.AddRange(server.ClusterConfiguration.Nodes.Where(n => !n.IsSlave)
                            .Select(n => n.EndPoint));
                        break;
                    }
                    // 单节点，主-从
                    if (server.ServerType == ServerType.Standalone & !server.IsSlave)
                    {
                        masters.Add(endPoint);
                        break;
                    }
                }
            }

            return masters;
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = System.Object;

namespace CatAsset.Runtime
{
    /// <summary>
    /// 批量资源加载完毕回调方法的原型
    /// </summary>
    public delegate void BatchAssetLoadedCallback(List<AssetHandler<object>> handlers);

    /// <summary>
    /// 批量资源句柄
    /// </summary>
    public class BatchAssetHandler : BaseHandler
    {
        /// <summary>
        /// 可等待对象
        /// </summary>
        public readonly struct Awaiter : INotifyCompletion
        {
            private readonly BatchAssetHandler handler;

            public Awaiter(BatchAssetHandler handler)
            {
                this.handler = handler;
            }
        
            public bool IsCompleted => handler.State == HandlerState.Success || handler.State == HandlerState.Failed;

            public List<AssetHandler<object>> GetResult()
            {
                return handler.Handlers;
            }
        
            public void OnCompleted(Action continuation)
            {
                handler.ContinuationCallBack = continuation;
            }
        }
        
        /// <summary>
        /// 需要加载的资源数量
        /// </summary>
        private int assetCount;

        /// <summary>
        /// 加载结束的资源数量
        /// </summary>
        private int loadedCount;

        /// <summary>
        /// 资源句柄列表，注意：会在加载结束调用完回调后被清空
        /// </summary>
        public List<AssetHandler<object>> Handlers { get; } = new List<AssetHandler<object>>();

        /// <summary>
        /// 资源加载完毕回调
        /// </summary>
        internal readonly AssetLoadedCallback<object> OnAssetLoadedCallback;

        /// <summary>
        /// 批量资源加载完毕回调
        /// </summary>
        private BatchAssetLoadedCallback onLoadedCallback;

        public BatchAssetHandler()
        {
            OnAssetLoadedCallback = OnAssetLoaded;
        }

        /// <summary>
        /// 资源加载完毕回调
        /// </summary>
        private void OnAssetLoaded(AssetHandler<object> handler)
        {
            loadedCount++;
            
            CheckLoaded();
        }

        /// <summary>
        /// 检查所有资源是否已加载完毕
        /// </summary>
        internal void CheckLoaded()
        {
            if (loadedCount == assetCount)
            {
                State = HandlerState.Success;
            
                onLoadedCallback?.Invoke(Handlers);
                ContinuationCallBack?.Invoke();
                
                //加载结束 释放句柄
                if (State == HandlerState.Success)
                {
                    Release();
                }
            }
        }



        /// <summary>
        /// 添加资源句柄
        /// </summary>
        internal void AddAssetHandler(AssetHandler<object> handler)
        {
            Handlers.Add(handler);
        }
        
        /// <inheritdoc />
        public override void Cancel()
        {
            if (State == HandlerState.InValid)
            {
                Debug.LogWarning($"取消了无效的{GetType().Name}");
                return;
            }
            
            foreach (AssetHandler<object> assetHandler in Handlers)
            {
                assetHandler.Dispose();
            }

            //释放自身
            Release();
        }

        /// <inheritdoc />
        public override void Unload()
        {
            if (State == HandlerState.InValid)
            {
                Debug.LogError($"卸载了无效的{GetType().Name}");
                return;
            }
            
            foreach (AssetHandler<object> assetHandler in Handlers)
            {
                if (State == HandlerState.InValid)
                {
                    continue;
                }

                assetHandler.Unload();
            }

            //释放自身
            Release();
        }

        /// <summary>
        /// 获取可等待对象
        /// </summary>
        public Awaiter GetAwaiter()
        {
            return new Awaiter(this);
        }
        
        public static BatchAssetHandler Create(int assetCount,BatchAssetLoadedCallback callback)
        {
            BatchAssetHandler handler = ReferencePool.Get<BatchAssetHandler>();
            handler.State = HandlerState.Doing;
            handler.assetCount = assetCount;
            handler.onLoadedCallback = callback;

            return handler;
        }

        public override void Clear()
        {
            base.Clear();

            assetCount = default;
            loadedCount = default;
            Handlers.Clear();
        }
    }
}
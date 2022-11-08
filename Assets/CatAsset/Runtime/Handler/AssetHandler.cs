﻿using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CatAsset.Runtime
{
    /// <summary>
    /// 资源加载完毕回调方法的原型
    /// </summary>
    public delegate void AssetLoadedCallback<T>(AssetHandler<T> handler);

    /// <summary>
    /// 自定义原生资源转换方法的原型
    /// </summary>
    public delegate object CustomRawAssetConverter(byte[] bytes);

    /// <summary>
    /// 资源句柄
    /// </summary>
    public abstract class AssetHandler : BaseHandler
    {
        /// <summary>
        /// 原始资源实例
        /// </summary>
        public object AssetObj { get; protected set; }

        /// <summary>
        /// 资源类别
        /// </summary>
        public AssetCategory Category { get; protected set; }

        /// <summary>
        /// 设置原始资源实例
        /// </summary>
        internal abstract void SetAsset(object loadedAsset);

        /// <inheritdoc />
        public override void Unload()
        {
            if (State == HandlerState.InValid)
            {
                Debug.LogError($"卸载了无效的{GetType().Name}");
                return;
            }
            
            CatAssetManager.UnloadAsset(AssetObj);
            Release();
        }

        /// <summary>
        /// 转换原始资源实例为指定类型的资源实例
        /// </summary>
        public T AssetAs<T>()
        {
            if (AssetObj == null)
            {
                return default;
            }

            Type type = typeof(T);

            if (type == typeof(object))
            {
                return (T)AssetObj;
            }

            switch (Category)
            {
                case AssetCategory.InternalBundledAsset:
                    if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                    {
                        if (type == typeof(Sprite) && AssetObj is Texture2D tex)
                        {
                            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
                            return (T) (object) sprite;
                        }
                        else if (type == typeof(Texture2D) && AssetObj is Sprite sprite)
                        {
                            return (T) (object) sprite.texture;
                        }
                        else
                        {
                            return (T)AssetObj;
                        }
                    }

                    Debug.LogError($"AssetHandler.AssetAs获取失败，资源类别为{Category}，但是T为{type}");
                    return default;

                case AssetCategory.InternalRawAsset:
                case AssetCategory.ExternalRawAsset:

                    if (type == typeof(byte[]))
                    {
                        return (T)AssetObj;
                    }

                    CustomRawAssetConverter converter = CatAssetManager.GetCustomRawAssetConverter(type);
                    if (converter == null)
                    {
                        Debug.LogError($"AssetHandler.AssetAs获取失败，没有注册类型{type}的CustomRawAssetConverter");
                        return default;
                    }

                    object convertedAsset = converter((byte[]) AssetObj);
                    return (T) convertedAsset;

            }

            return default;
        }

        public override void Clear()
        {
            base.Clear();

            AssetObj = default;
            Category = default;
        }
    }


    /// <inheritdoc />
    public class AssetHandler<T> : AssetHandler
    {
        /// <summary>
        /// 可等待对象
        /// </summary>
        public readonly struct Awaiter : INotifyCompletion
        {
            private readonly AssetHandler<T> handler;

            public Awaiter(AssetHandler<T> handler)
            {
                this.handler = handler;
            }
        
            public bool IsCompleted => handler.State == HandlerState.Success || handler.State == HandlerState.Failed;

            public T GetResult()
            {
                return handler.Asset;
            }
        
            public void OnCompleted(Action continuation)
            {
                handler.ContinuationCallBack = continuation;
            }
        }
        
        /// <summary>
        /// 资源实例
        /// </summary>
        public T Asset { get; private set; }

        /// <summary>
        /// 资源加载完毕回调
        /// </summary>
        private AssetLoadedCallback<T> onLoadedCallback;

        /// <inheritdoc />
        internal override void SetAsset(object loadedAsset)
        {
            AssetObj = loadedAsset;
            Asset = AssetAs<T>();

            State = AssetObj != null ? HandlerState.Success : HandlerState.Failed;
            
            onLoadedCallback?.Invoke(this);
            ContinuationCallBack?.Invoke();

            if (State == HandlerState.Failed)
            {
                //加载失败 自行释放句柄
                Release();
            }
        }

        /// <summary>
        /// 获取可等待对象
        /// </summary>
        public Awaiter GetAwaiter()
        {
            return new Awaiter(this);
        }
        
        public static AssetHandler<T> Create(string name, AssetLoadedCallback<T> callback,AssetCategory category = AssetCategory.None)
        {
            AssetHandler<T> handler = ReferencePool.Get<AssetHandler<T>>();
            handler.Name = name;
            handler.State = HandlerState.Doing;
            handler.Category = category;
            handler.onLoadedCallback = callback;
            return handler;
        }

        public override void Clear()
        {
            base.Clear();

            Asset = default;
            onLoadedCallback = default;
        }


    }
}
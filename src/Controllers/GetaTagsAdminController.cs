﻿// Copyright (c) Geta Digital. All rights reserved.
// Licensed under Apache-2.0. See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web.Mvc;
using EPiServer;
using EPiServer.Core;
using EPiServer.Data;
using EPiServer.DataAccess;
using EPiServer.Security;
using EPiServer.ServiceLocation;
using EPiServer.Shell;
using Geta.Tags.Interfaces;
using Geta.Tags.Models;
using PagedList;

namespace Geta.Tags.Controllers
{
    [Authorize(Roles = "Administrators, WebAdmins, CmsAdmins")]
    [EPiServer.PlugIn.GuiPlugIn(
        Area = EPiServer.PlugIn.PlugInArea.AdminMenu,
        Url = "/GetaTagsAdmin",
        DisplayName = "Geta Tags Management")]
    public class GetaTagsAdminController : Controller
    {
        public static int PageSize { get; } = 30;

        private readonly ITagRepository _tagRepository;
        private readonly IContentRepository _contentRepository;
        private readonly ITagEngine _tagEngine;

        public GetaTagsAdminController() : this(
                ServiceLocator.Current.GetInstance<ITagRepository>(),
                ServiceLocator.Current.GetInstance<IContentRepository>(),
                ServiceLocator.Current.GetInstance<ITagEngine>())
        {
        }

        public GetaTagsAdminController(
            ITagRepository tagRepository, IContentRepository contentRepository, ITagEngine tagEngine)
        {
            _tagRepository = tagRepository;
            _contentRepository = contentRepository;
            _tagEngine = tagEngine;
        }

        public ActionResult Index(string searchString, int? page)
        {
            var pageNumber = page ?? 1;
            var tags = _tagRepository.GetAllTags().ToList();
            ViewBag.TotalCount = tags.Count;

            if (string.IsNullOrEmpty(searchString) && (page == null || page == pageNumber))
            {
                return View(GetViewPath("Index"), GetPagedTagList(tags, pageNumber, PageSize));
            }

            ViewBag.SearchString = searchString;
            tags = _tagRepository.GetAllTags().Where(s => s.Name.Contains(searchString)).ToList();

            return View(GetViewPath("Index"), GetPagedTagList(tags, pageNumber, PageSize));
        }

        private IPagedList<Tag> GetPagedTagList(IList<Tag> tags, int pageNumber, int pageSize)
        {
            var list = tags.ToPagedList(pageNumber, PageSize);
            if (!list.Any() && pageNumber > 1)
            {
                return tags.ToPagedList(pageNumber - 1, PageSize);
            }
            return list;
        }

        public ActionResult Edit(string tagId, int? page, string searchString)
        {
            if (tagId == null)
            {
                return HttpNotFound();
            }

            var tag = _tagRepository.GetTagById(Identity.Parse(tagId));
            if (tag == null)
            {
                return HttpNotFound();
            }

            ViewBag.Page = page;
            ViewBag.SearchString = searchString;
            return PartialView(GetViewPath("Edit"), tag);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(string id, Tag eddittedTag, int? page, string searchString)
        {
            if (id == null)
            {
                return HttpNotFound();
            }

            var existingTag = _tagRepository.GetTagById(Identity.Parse(id));

            if (existingTag == null)
            {
                return RedirectToAction("Index", new { page, searchString });
            }

            if (eddittedTag.checkedEditContentTags)
            {
                EditTagsInContentRepository(existingTag, eddittedTag);
            }

            existingTag.Name = eddittedTag.Name;
            existingTag.GroupKey = eddittedTag.GroupKey;
            _tagRepository.Save(existingTag);

            return RedirectToAction("Index", new { page, searchString });
        }

        public void EditTagsInContentRepository(Tag tagFromTagRepository, Tag tagFromUser)
        {
            var existingTagName = tagFromTagRepository.Name;
            var contentReferencesFromTag = _tagEngine.GetContentReferencesByTags(existingTagName);

            foreach (var item in contentReferencesFromTag)
            {
                var pageFromRepository = (ContentData)_contentRepository.Get<IContent>(item);

                var clone = pageFromRepository.CreateWritableClone();

                var tagAttributes = clone.GetType().GetProperties().Where(
                   prop => Attribute.IsDefined(prop, typeof(UIHintAttribute)) &&
                   prop.PropertyType == typeof(string) &&
                   Attribute.GetCustomAttributes(prop, typeof(UIHintAttribute)).Any(x => ((UIHintAttribute)x).UIHint == "Tags"));

                foreach (var tagAttribute in tagAttributes)
                {
                    var tags = tagAttribute.GetValue(clone) as string;
                    if (string.IsNullOrEmpty(tags)) continue;

                    var pageTagList = tags.Split(',').ToList();
                    var indexTagToReplace = pageTagList.IndexOf(existingTagName);

                    if (indexTagToReplace == -1) continue;
                    pageTagList[indexTagToReplace] = tagFromUser.Name;

                    var tagsCommaSeperated = string.Join(",", pageTagList);

                    tagAttribute.SetValue(clone, tagsCommaSeperated);
                }
                _contentRepository.Save((IContent)clone, SaveAction.Publish, AccessLevel.NoAccess);
            }
            _tagRepository.Delete(tagFromTagRepository);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string tagId, int? page, string searchString)
        {
            if (tagId != null)
            {
                var existingTag = _tagRepository.GetTagById(Identity.Parse(tagId));

                _tagRepository.Delete(existingTag);

                ViewBag.Page = page;
                ViewBag.SearchString = searchString;
                return RedirectToAction("Index", new { page, searchString });
            }

            return View(GetViewPath("Delete"));
        }

        private string GetViewPath(string viewName)
        {
            return Paths.ToClientResource(typeof(GetaTagsAdminController), "Views/Admin/" + viewName + ".cshtml");
        }
    }
}
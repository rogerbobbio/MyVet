﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyVet.Web.Data;
using MyVet.Web.Data.Entities;
using MyVet.Web.Helpers;
using MyVet.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyVet.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class OwnersController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly IUserHelper _userHelper;
        private readonly ICombosHelper _combosHelper;
        private readonly IConverterHelper _converterHelper;
        private readonly IImageHelper _imageHelper;

        public OwnersController(DataContext context, IUserHelper userHelper,
                                                     ICombosHelper combosHelper,
                                                     IConverterHelper converterHelper,
                                                     IImageHelper imageHelper)
        {
            _dataContext = context;
            _userHelper = userHelper;
            _combosHelper = combosHelper;
            _converterHelper = converterHelper;
            _imageHelper = imageHelper;
        }

        public IActionResult Index()
        {
            return View(_dataContext.Owners
                 .Include(o => o.User)
                 .Include(o => o.Pets));
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }


            var owner = await _dataContext.Owners
                .Include(o => o.User)
                .Include(o => o.Pets)
                .ThenInclude(p => p.PetType)
                .Include(o => o.Pets)
                .ThenInclude(p => p.Histories)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (owner == null)
            {
                return NotFound();
            }

            return View(owner);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AddUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await AddUser(model);
                if (user == null)
                {
                    return View(model);
                }

                var owner = new Owner
                {
                    Agendas = new List<Agenda>(),
                    Pets = new List<Pet>(),
                    User = user,
                };

                _dataContext.Owners.Add(owner);
                try
                {
                    await _dataContext.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, ex.ToString());
                    return View(model);
                }
            }
            return View(model);
        }

        private async Task<User> AddUser(AddUserViewModel view)
        {
            var user = new User
            {
                Address = view.Address,
                Document = view.Document,
                Email = view.Username,
                FirstName = view.FirstName,
                LastName = view.LastName,
                PhoneNumber = view.PhoneNumber,
                UserName = view.Username
            };

            var result = await _userHelper.AddUserAsync(user, view.Password);
            if (result != IdentityResult.Success)
            {
                ModelState.AddModelError(string.Empty, result.Errors.FirstOrDefault().Description);
                return null;
            }

            var newUser = await _userHelper.GetUserByEmailAsync(view.Username);
            await _userHelper.AddUserToRoleAsync(newUser, "Customer");
            return newUser;
        }


        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var owner = await _dataContext.Owners.FindAsync(id);
            if (owner == null)
            {
                return NotFound();
            }
            return View(owner);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id")] Owner owner)
        {
            if (id != owner.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _dataContext.Update(owner);
                    await _dataContext.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OwnerExists(owner.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(owner);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var owner = await _dataContext.Owners
                .Include(p => p.User)
                .Include(p => p.Pets)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (owner == null)
            {
                return NotFound();
            }

            if (owner.Pets.Count > 0)
            {
                ModelState.AddModelError(string.Empty, "The Owner can't be deleted because it has related records.");
                return RedirectToAction(nameof(Index));
            }

            await _userHelper.DeleteUserAsync(owner.User.Email);

            _dataContext.Owners.Remove(owner);
            await _dataContext.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }        

        private bool OwnerExists(int id)
        {
            return _dataContext.Owners.Any(e => e.Id == id);
        }

        public async Task<IActionResult> AddPet(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var owner = await _dataContext.Owners.FindAsync(id.Value);
            if (owner == null)
            {
                return NotFound();
            }

            var petViewModel = new PetViewModel
            {
                Born = DateTime.Today,
                OwnerId = owner.Id,
                PetTypes = _combosHelper.GetComboPetTypes()
            };

            return View(petViewModel);
        }

        [HttpPost]
        public async Task<IActionResult> AddPet(PetViewModel model)
        {
            if (ModelState.IsValid)
            {
                var path = string.Empty;

                if (model.ImageFile != null)
                {
                    path = await _imageHelper.UploadImageAsync(model.ImageFile);
                }

                var pet = await _converterHelper.ToPetAsync(model, path, true);
                _dataContext.Pets.Add(pet);
                await _dataContext.SaveChangesAsync();
                return RedirectToAction($"Details/{model.OwnerId}");
            }

            model.PetTypes = _combosHelper.GetComboPetTypes();
            return View(model);
        }

        public async Task<IActionResult> EditPet(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pet = await _dataContext.Pets
                .Include(p => p.Owner)
                .Include(p => p.PetType)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (pet == null)
            {
                return NotFound();
            }

            return View(_converterHelper.ToPetViewModel(pet));
        }

        [HttpPost]
        public async Task<IActionResult> EditPet(PetViewModel model)
        {
            if (ModelState.IsValid)
            {
                var path = model.ImageUrl;

                if (model.ImageFile != null)
                {
                    path = await _imageHelper.UploadImageAsync(model.ImageFile);
                }

                var pet = await _converterHelper.ToPetAsync(model, path, false);
                _dataContext.Pets.Update(pet);
                await _dataContext.SaveChangesAsync();
                return RedirectToAction($"Details/{model.OwnerId}");
            }

            model.PetTypes = _combosHelper.GetComboPetTypes();
            return View(model);
        }

        public async Task<IActionResult> DetailsPet(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pet = await _dataContext.Pets
                .Include(p => p.Owner)
                .ThenInclude(o => o.User)
                .Include(p => p.Histories)
                .ThenInclude(h => h.ServiceType)
                .FirstOrDefaultAsync(o => o.Id == id.Value);
            if (pet == null)
            {
                return NotFound();
            }

            return View(pet);
        }

        public async Task<IActionResult> AddHistory(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pet = await _dataContext.Pets.FindAsync(id.Value);
            if (pet == null)
            {
                return NotFound();
            }

            var model = new HistoryViewModel
            {
                Date = DateTime.Now,
                PetId = pet.Id,
                ServiceTypes = _combosHelper.GetComboServiceTypes(),
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AddHistory(HistoryViewModel model)
        {
            if (ModelState.IsValid)
            {
                var history = await _converterHelper.ToHistoryAsync(model, true);
                _dataContext.Histories.Add(history);
                await _dataContext.SaveChangesAsync();
                return RedirectToAction($"{nameof(DetailsPet)}/{model.PetId}");
            }

            model.ServiceTypes = _combosHelper.GetComboServiceTypes();
            return View(model);
        }

        public async Task<IActionResult> EditHistory(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var history = await _dataContext.Histories
                .Include(h => h.Pet)
                .Include(h => h.ServiceType)
                .FirstOrDefaultAsync(p => p.Id == id.Value);
            if (history == null)
            {
                return NotFound();
            }

            return View(_converterHelper.ToHistoryViewModel(history));
        }

        [HttpPost]
        public async Task<IActionResult> EditHistory(HistoryViewModel model)
        {
            if (ModelState.IsValid)
            {
                var history = await _converterHelper.ToHistoryAsync(model, false);
                _dataContext.Histories.Update(history);
                await _dataContext.SaveChangesAsync();
                return RedirectToAction($"{nameof(DetailsPet)}/{model.PetId}");
            }

            model.ServiceTypes = _combosHelper.GetComboServiceTypes();
            return View(model);
        }

        public async Task<IActionResult> DeleteHistory(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var history = await _dataContext.Histories.Include(h => h.Pet)
                                                      .FirstOrDefaultAsync(h => h.Id == id.Value);
            if (history == null)
            {
                return NotFound();
            }

            _dataContext.Histories.Remove(history);
            await _dataContext.SaveChangesAsync();
            return RedirectToAction($"{nameof(DetailsPet)}/{history.Pet.Id}");
        }

        public async Task<IActionResult> DeletePet(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pet = await _dataContext.Pets.Include(p => p.Owner)
                                             .Include(h => h.Histories)
                                             .FirstOrDefaultAsync(p => p.Id == id.Value);
            if (pet == null)
            {
                return NotFound();
            }

            if (pet.Histories.Count > 0)
            {
                ModelState.AddModelError(string.Empty, "The pet can't be deleted because it has related records.");
                return RedirectToAction($"{nameof(Details)}/{pet.Owner.Id}");
            }

            _dataContext.Pets.Remove(pet);
            await _dataContext.SaveChangesAsync();
            return RedirectToAction($"{nameof(Details)}/{pet.Owner.Id}");
        }
    }
}

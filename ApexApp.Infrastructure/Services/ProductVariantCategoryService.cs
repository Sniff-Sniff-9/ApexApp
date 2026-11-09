using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Infrastructure.Services
{
    public class ProductVariantCategoryService
    {
        private readonly AppDbContext context;

        public ProductVariantCategoryService(AppDbContext context)
        {
            this.context = context;
        }

        public async Task<List<ProductVariantCategory>> GetAllAsync(
        CancellationToken cancellationToken = default)
        {
            return await context.Set<ProductVariantCategory>()
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<ProductVariantCategory?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            if (id <= 0)
                return null;

            return await context.Set<ProductVariantCategory>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.Id == id && c.IsActive,
                    cancellationToken);
        }

        public async Task<(bool Success, string? Error, ProductVariantCategory? Data)> CreateAsync(
            ProductVariantCategory category,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
                return (false, "Category name is required.", null);

            var name = category.Name.Trim();

            var exists = await context.Set<ProductVariantCategory>()
                .AnyAsync(c => c.Name.ToLower() == name.ToLower(),
                    cancellationToken);

            if (exists)
                return (false, "Category already exists.", null);

            var now = DateTime.UtcNow;

            category.Name = name;
            category.IsActive = true;

            await context.Set<ProductVariantCategory>().AddAsync(category, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return (true, null, category);
        }

        public async Task<(bool Success, string? Error, ProductVariantCategory? Data)> UpdateAsync(
            int id,
            ProductVariantCategory input,
            CancellationToken cancellationToken = default)
        {
            if (id <= 0)
                return (false, "Invalid category ID.", null);

            if (string.IsNullOrWhiteSpace(input.Name))
                return (false, "Category name is required.", null);

            var category = await context.Set<ProductVariantCategory>()
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (category is null)
                return (false, "Category was not found.", null);

            var name = input.Name.Trim();

            var exists = await context.Set<ProductVariantCategory>()
                .AnyAsync(c => c.Id != id &&
                               c.Name.ToLower() == name.ToLower(),
                    cancellationToken);

            if (exists)
                return (false, "Category already exists.", null);

            category.Name = name;
            category.IsActive = input.IsActive;

            await context.SaveChangesAsync(cancellationToken);

            return (true, null, category);
        }

        public async Task<(bool Success, string? Error)> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            if (id <= 0)
                return (false, "Invalid category ID.");

            var category = await context.Set<ProductVariantCategory>()
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (category is null)
                return (false, "Category was not found.");

            var hasProducts = await context.Set<ProductVariant>()
                .AnyAsync(p => p.CategoryId == id, cancellationToken);

            if (hasProducts)
                return (false, "Cannot delete category because it has products.");

            category.IsActive = false;

            await context.SaveChangesAsync(cancellationToken);

            return (true, null);
        }
    }
}

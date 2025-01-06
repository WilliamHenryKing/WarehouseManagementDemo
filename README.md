# SCAD Software Demo

## Warehouse Management System

A **Warehouse Management System** built with the following specifications:

### API Specifications
A **REST-ful .NET-based API in C#** that provides the following functionalities:

1. **Products Management**
   - **List Products**: Retrieve a list of all products.
   - **Create Products**: Add new products with the following fields:
     - `code`: Unique identifier for the product (must be unique).
     - `description`: Description of the product.

2. **Warehouses Management**
   - **List Warehouses**: Retrieve a list of all warehouses.
   - **Create Warehouses**: Add new warehouses with the following fields:
     - `code`: Unique identifier for the warehouse (must be unique).
     - `name`: Name of the warehouse.

3. **Order Management**
   - **Create Orders**: Facilitate the movement of product quantities between warehouses:
     - **Source Warehouse**: Reduce the quantity of products.
     - **Destination Warehouse**: Increase the quantity of products.

4. **Inventory Query**
   - **List On-Hand Products**: Retrieve products available in warehouses.
     - Queryable by:
       - Product Code.
       - Warehouse Code.

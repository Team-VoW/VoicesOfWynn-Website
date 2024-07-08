# Use an official PHP image with Apache
FROM php:7.4-apache

# Install necessary PHP extensions and Composer
RUN apt-get update && apt-get install -y \
    libzip-dev \
    unzip \
    && docker-php-ext-install mysqli pdo pdo_mysql zip \
    && curl -sS https://getcomposer.org/installer | php -- --install-dir=/usr/local/bin --filename=composer

# Set working directory
WORKDIR /var/www/html

# Copy the application code to the container
COPY . .

# Install PHP dependencies
RUN composer install

# Set permissions (optional but recommended)
RUN chown -R www-data:www-data /var/www/html && \
    chmod -R 755 /var/www/html

# Enable Apache mod_rewrite (if needed for .htaccess)
RUN a2enmod rewrite

# Copy custom php.ini and create sessions directory
COPY php.ini /usr/local/etc/php/conf.d/
RUN mkdir -p /var/www/html/sessions && \
    chown -R www-data:www-data /var/www/html/sessions && \
    chmod -R 777 /var/www/html/sessions

# Restart Apache to ensure all configurations are applied
RUN service apache2 restart

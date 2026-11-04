terraform {
  required_version = ">= 1.10"

  backend "s3" {
    bucket       = "restaurant-terraform-state-124021120581"
    key          = "restaurant/terraform.tfstate"
    region       = "us-west-2"
    use_lockfile = true
    encrypt      = true
  }
}
